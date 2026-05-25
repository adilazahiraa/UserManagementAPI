namespace UserManagementAPI.Services;

public class MyService : IMyService
{
    private readonly int _serviceId;

    public MyService()
    {
        _serviceId = Random.Shared.Next(100000, 999999);
    }

    public void LogCreation(string message)
    {
        Console.WriteLine($"{message} - Service ID: {_serviceId}");
    }
}