using Locatic.Models;

namespace Locatic.Interfaces;

public interface IClientRepository
{
    IEnumerable<Client> GetAll();
    Client? GetById(int id);
    void Add(Client client);
    void SaveChanges();
}
