using BCrypt.Net;
using System;

class Program
{
    static void Main(string[] args)
    {
        string password = "Kis@1963";
        string hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
        Console.WriteLine($"Password: {password}");
        Console.WriteLine($"Hash: {hash}");
    }
}
