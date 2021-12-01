﻿// See https://aka.ms/new-console-template for more information
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
try
{
    while (await timer.WaitForNextTickAsync(cts.Token))
    {
        Console.WriteLine($"Timed event triggered({DateTime.Now:HH:mm:ss})");
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation cancelled");
}

// Dispose
var timer1 = new PeriodicTimer(TimeSpan.FromSeconds(2));
timer1.Dispose();
if (await timer1.WaitForNextTickAsync())
{
    Console.WriteLine("Timer1 event triggered");
}

Console.WriteLine("Hello world");
