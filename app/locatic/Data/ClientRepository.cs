using Locatic.Data.Context;
using Locatic.Interfaces;
using Locatic.Models;

namespace Locatic.Data;

public class ClientRepository : IClientRepository
{
    private readonly AppDbContext _context;

    public ClientRepository(AppDbContext context)
    {
        _context = context;
    }

    public IEnumerable<Client> GetAll()
        => _context.Clients.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ToList();

    public Client? GetById(int id)
        => _context.Clients.Find(id);

    public void Add(Client client)
        => _context.Clients.Add(client);

    public void SaveChanges()
        => _context.SaveChanges();
}
