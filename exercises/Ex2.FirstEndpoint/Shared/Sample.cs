using Contoso.Messaging.Contracts;

namespace Exercise2;

public static class Sample
{
    public static ShipOrder NewShipOrder() =>
        new(Guid.NewGuid(), new Address("Lyngby Hovedgade 1", "Kongens Lyngby", "2800", "DK"));

    public static string Short(this Guid id) => id.ToString()[..8];
}
