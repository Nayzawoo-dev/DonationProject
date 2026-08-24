using Microsoft.AspNetCore.Identity;

var hasher = new PasswordHasher<object>();

string password = "nzo#1385";

string hash = hasher.HashPassword(null!, password);

Console.WriteLine(hash);
