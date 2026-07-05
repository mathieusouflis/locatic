# Preuves d'exécution

Captures d'écran, extraits de logs et exports montrant que les étapes importantes fonctionnent vraiment, pas juste que le code existe.

Checklist des preuves à rassembler :

- [x] [Règles de protection de `main` (Settings → Branches)](./images/branches.png)
- [x] [Une Pull Request représentative : checks CI verts, merge bloqué tant que ce n'est pas validé](./images/CI.png)
- [x] [Le pipeline GitHub Actions complet (commitlint → test → build-app → build-image → security)](./images/CI.png)
- [x] [L'image publiée sur GHCR (onglet Packages)](./images/ghcr.png)
- [x] [`terraform plan` / `apply`](./images/terraform_apply.png) et [`terraform output`](./images/terraform_output.png)
- [x] [L'exécution du playbook Ansible](./images/ansible_playbook.png)
- [x] [`kubectl get all -n locatic` (tout doit être Running)](./images/kubectl_get_all.png)
- [x] L'accès à l'application via l'URL Nginx (`minikube service -n locatic nginx --url`) [img1](./images/locatic_nginx_url.png) [img2](./images/locatic_nginx_url2.png)
- [x] La persistance SQLite : une donnée créée, le pod supprimé, la donnée toujours là au redémarrage
- [x] Le dashboard Grafana couvrant app / Nginx / stockage / monitoring (`minikube service -n monitoring monitoring-grafana --url`) [Dashboard Locatic](./images/graphana_dashboard.png) [Dashboard with App down](./images/graphana_dashboard_down.png)
- [x] [Une alerte testée (LocaticDown qui passe en Firing puis se résout)](./images/prometeus_alert.png)
- [ ] Le rollback démontré (bonus)
