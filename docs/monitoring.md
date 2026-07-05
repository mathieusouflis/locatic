# Monitoring (Prometheus & Grafana)

## Ce qui est surveillé

| Service | Source de métriques | Indicateurs clés |
|---------|--------------------|------------------|
| Application Locatic | endpoint `/metrics` (prometheus-net) | requêtes HTTP, latence, erreurs 5xx, `up` |
| Nginx | `stub_status` via nginx-prometheus-exporter (port 9113) | connexions actives, requêtes/s, `up` |
| Pods & services K8s | kube-state-metrics (inclus dans la stack) | statut des pods, restarts, replicas prêts |
| Stockage SQLite | kubelet (`kubelet_volume_stats_*`) | espace utilisé / disponible du PVC |
| Monitoring lui-même | Prometheus & Grafana s'auto-exposent | `up`, cibles en échec |

## Étape 1 : exposer les métriques de l'application

```bash
dotnet add app/locatic package prometheus-net.AspNetCore
```

Dans `app/locatic/Program.cs` :

```csharp
using Prometheus;                     // en haut du fichier

// après app.UseRouting();
app.UseHttpMetrics();                 // compte et chronomètre chaque requête HTTP

// à côté de app.MapHealthChecks("/health");
app.MapMetrics();                     // expose /metrics au format Prometheus
```

Vérification : `dotnet run --project app/locatic` puis `curl http://localhost:5118/metrics` (on doit voir `http_requests_received_total`, `http_request_duration_seconds`). Les annotations `prometheus.io/*` du Deployment (voir [kubernetes.md](kubernetes.md)) permettent à Prometheus de découvrir le pod automatiquement.

## Étape 2 : déployer la stack de monitoring

Chart Helm `kube-prometheus-stack` (Prometheus, Grafana, kube-state-metrics, node-exporter préconfigurés), intégré au playbook Ansible :

```yaml
- name: Ajouter le repo prometheus-community
  kubernetes.core.helm_repository:
    name: prometheus-community
    repo_url: https://prometheus-community.github.io/helm-charts

- name: Déployer la stack de monitoring
  kubernetes.core.helm:
    name: monitoring
    chart_ref: prometheus-community/kube-prometheus-stack
    release_namespace: monitoring
    create_namespace: true
    values:
      grafana:
        adminPassword: "{{ grafana_admin_password }}"   # variable locale, jamais commitée
      prometheus:
        prometheusSpec:
          additionalScrapeConfigs:
            - job_name: locatic-pods
              kubernetes_sd_configs: [{ role: pod, namespaces: { names: [locatic] } }]
              relabel_configs:
                - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_scrape]
                  action: keep
                  regex: "true"
                - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_port]
                  target_label: __address__
                  regex: (.+)
                  replacement: ${1}
                - source_labels: [__address__, __meta_kubernetes_pod_annotation_prometheus_io_port]
                  action: replace
                  regex: ([^:]+)(?::\d+)?;(\d+)
                  replacement: $1:$2
                  target_label: __address__
```

## Étape 3 : accéder aux interfaces

```bash
# Prometheus
kubectl port-forward svc/monitoring-kube-prometheus-prometheus 9090:9090 -n monitoring
open http://localhost:9090/targets     # toutes les cibles doivent être UP

# Grafana
kubectl port-forward svc/monitoring-grafana 3000:80 -n monitoring
open http://localhost:3000             # admin / <grafana_admin_password>
```

## Étape 4 : le dashboard "état des services"

Requêtes PromQL utiles pour voir en un coup d'oeil si Nginx, l'application, le stockage et le monitoring fonctionnent :

| Panel | Requête PromQL |
|-------|----------------|
| App disponible | `up{job="locatic-pods", namespace="locatic"}` |
| Latence app (p95) | `histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))` |
| Erreurs 5xx app | `rate(http_requests_received_total{code=~"5.."}[5m])` |
| Nginx disponible | `nginx_up` |
| Trafic Nginx | `rate(nginx_http_requests_total[5m])` |
| Pods non prêts | `kube_deployment_status_replicas_unavailable{namespace="locatic"}` |
| Restarts | `increase(kube_pod_container_status_restarts_total{namespace="locatic"}[1h])` |
| Espace PVC SQLite | `kubelet_volume_stats_available_bytes{namespace="locatic"} / kubelet_volume_stats_capacity_bytes{namespace="locatic"}` |

Export JSON du dashboard gardé dans `docs/preuves/`.

### Dashboard provisionné automatiquement

`infra/ansible/roles/monitoring/files/locatic-dashboard.json` contient un dashboard chargé automatiquement au déploiement (pas besoin de le recréer à la main dans Grafana). Le rôle `monitoring` crée une ConfigMap `locatic-dashboard` labellisée `grafana_dashboard: "1"` dans le namespace `monitoring` ; le sidecar Grafana de kube-prometheus-stack surveille ce label et charge le JSON.

Le dashboard tient sur un écran, 4 stats en haut puis les graphes :

| Panel | Contenu |
|-------|---------|
| App Locatic (stat) | `up` de l'app (port 8080), vert/rouge |
| Nginx (stat) | `up` de l'exporter nginx (port 9113), vert/rouge |
| PVC locatic-sqlite : % utilisé (stat) | `kubelet_volume_stats_used_bytes / kubelet_volume_stats_capacity_bytes * 100`, seuils à 70% et 80% |
| Cibles Prometheus saines (stat) | `count(up == 1)`, nombre de cibles up sur tout le cluster |
| App : requêtes/s | `rate(http_request_duration_seconds_count[5m])` |
| App : requêtes en cours | `http_requests_in_progress` |
| Nginx : connexions actives | `nginx_connections_active` |
| Nginx : requêtes/s | `rate(nginx_http_requests_total[5m])` |
| Stockage SQLite : utilisé vs capacité | les deux séries `kubelet_volume_stats_used_bytes` et `kubelet_volume_stats_capacity_bytes` sur le même graphe |

> **Les panneaux « PVC » exigent un volume CSI.** Les métriques
> `kubelet_volume_stats_*` ne sont émises que pour des PVC portés par un driver
> CSI. Le provisioner minikube par défaut (`standard`, hostPath) n'en produit
> **aucune** → panneaux « No Data ». On déploie donc le PVC sur la StorageClass
> `csi-hostpath-sc` (`minikube addons enable csi-hostpath-driver`, voir
> [deploiement-local.md](deploiement-local.md)).
>
> Caveat minikube : le driver CSI hostpath fait un `statfs` sur le **système de
> fichiers du nœud**, donc `capacity_bytes` vaut la taille du disque du nœud
> (≈ centaines de Go) et non les 256Mi demandés. Localement le `%` reflète donc
> le remplissage du disque du nœud ; sur un vrai CSI cloud (EBS…), la même requête
> reflète bien la capacité du PVC. L'alerte `SqlitePvcAlmostFull` (> 80 %) reste
> valable dans les deux cas.

L'app et l'exporter nginx sont scrapés par le même job `locatic-pods` (namespace `locatic`), donc je distingue les deux via `instance` (le port : `:8080` pour l'app, `:9113` pour nginx) plutôt que via `job`.

### Les alertes (PrometheusRule)

Le rôle `monitoring` déploie une ressource `PrometheusRule` (`locatic-alerts`, namespace `monitoring`, label `release: monitoring` pour que le Prometheus de la stack la sélectionne) :

| Alerte | Condition | Durée |
|--------|-----------|-------|
| `LocaticAppDown` | `up{job="locatic-pods", instance=~".*:8080"} == 0` | 2m |
| `NginxDown` | `up{job="locatic-pods", instance=~".*:9113"} == 0` | 2m |
| `SqlitePvcAlmostFull` | `kubelet_volume_stats_used_bytes / kubelet_volume_stats_capacity_bytes > 0.8` (pvc locatic-sqlite) | 5m |

Les trois sont en `severity: warning`, avec un `summary` et une `description` courts.

## Étape 5 : alertes simples (bonus +1)

```yaml
groups:
  - name: locatic
    rules:
      - alert: LocaticDown
        expr: up{job="locatic-pods"} == 0
        for: 2m
        annotations: { summary: "L'application Locatic ne répond plus" }
      - alert: NginxDown
        expr: nginx_up == 0
        for: 2m
        annotations: { summary: "Nginx ne répond plus" }
      - alert: SqliteVolumePresqueRempli
        expr: kubelet_volume_stats_available_bytes{namespace="locatic"} / kubelet_volume_stats_capacity_bytes{namespace="locatic"} < 0.10
        for: 5m
        annotations: { summary: "Moins de 10% d'espace libre sur le volume SQLite" }
      - alert: PodRestartsFrequents
        expr: increase(kube_pod_container_status_restarts_total{namespace="locatic"}[15m]) > 3
        annotations: { summary: "Un pod redémarre en boucle" }
```

Pour tester une alerte (et en garder une preuve) : `kubectl scale deployment/locatic --replicas=0 -n locatic`, attendre 2 minutes, `LocaticDown` doit passer en *Firing* dans l'onglet Alerts de Prometheus. Rétablir ensuite avec `--replicas=1`.
