using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Shared;

namespace KubeTunnel;

internal class Program
{

    public static async Task Main(string[] args)
    {
        // Create a flag to track when Ctrl+C is pressed.
        var isExitRequested = false;

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true; // Prevents the program from terminating immediately
            isExitRequested = true;
            Console.WriteLine("\nCtrl+C detected. Exiting...");
        };

        var currentProfile = LoadProfileConfig();

        Console.WriteLine($"Using profile: {currentProfile}");

        if (string.IsNullOrWhiteSpace(currentProfile))
            return;

        var configs = LoadConfiguration(currentProfile);

        if (configs == null || !configs.Any())
        {
            Console.WriteLine("No configured services");
            return;
        }

        var cancellationTokenSources = new List<CancellationTokenSource>();
        var tasks = new List<Task>();
        var processes = new ConcurrentBag<Process>();

        foreach (var config in configs)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSources.Add(cancellationTokenSource);

            var task = Task.Run(async () =>
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    if (isExitRequested)
                        break;

                    var (result, process) = StartKubectlPortForward(config);

                    switch (result)
                    {
                        case PortForwardResult.Success:
                            if (process != null)
                            {
                                await WaitForDisconnectionOrCancellation(process, cancellationTokenSource.Token);
                                processes.Add(process);
                            }
                            Console.WriteLine($"Connection for service '{config.Service}' was lost. Retrying...");
                            break;

                        case PortForwardResult.RetryableFailure:
                            Console.WriteLine(
                                $"Failed to port-forward service '{config.Service}' due to a retryable error. Retrying in 10 seconds...");
                            await Task.Delay(10000, cancellationTokenSource.Token);
                            break;

                        case PortForwardResult.PermanentFailure:
                            Console.WriteLine(
                                $"Failed to port-forward service '{config.Service}' due to a permanent error. Skipping this service.");
                            cancellationTokenSource.Cancel();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }, cancellationTokenSource.Token);

            tasks.Add(task);
        }

        Console.WriteLine("Press ctrl + C to cancel all port forwards and exit gracefully (you can also close it with the configuration tool)...");
        // Replace your existing while loop
        while (!isExitRequested)
        {
            await Task.Delay(500); // This is to prevent CPU hogging
        }

        Console.WriteLine("\nCancelling...");

        // Cancel all tasks
        cancellationTokenSources.ForEach(c => c.Cancel());

        // Await all tasks to completion before exiting
        await Task.WhenAll(tasks);

        Console.WriteLine("All port forwards cancelled. Exiting...");
    }

    static string? LoadProfileConfig()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KubeTunnelConfig");
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        var profilePath = Path.Combine(folder, "config.json");
        try
        {

            var profileContent = File.ReadAllText(profilePath);
            var config = JsonSerializer.Deserialize<Config>(profileContent) ?? new Config();
            return config.CurrentProfile;
        }
        catch (Exception)
        {
            Console.WriteLine("Could not get current profile, run the configuration tool first");
            return string.Empty;
        }
    }

    static PortForwardConfig[]? LoadConfiguration(string profile)
    {
        try
        {
            var profilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KubeTunnelConfig", "Profiles", $"{profile}.json");

            var configContent = File.ReadAllText(profilePath);
            return JsonSerializer.Deserialize<PortForwardConfig[]>(configContent);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }
    
    static (PortForwardResult, Process?) StartKubectlPortForward(PortForwardConfig config)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "kubectl",
            Arguments = $"port-forward svc/{config.Service} {config.LocalPort}:{config.RemotePort} -n {config.Namespace}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    
        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
    
        try
        {
            var processStarted = process.Start();

            if (processStarted)
            {
                Console.WriteLine($"Forwarding: '{config.Service}@{config.LocalPort}'...");
                return (PortForwardResult.Success, process);
            }

            Console.WriteLine($"Failed to start port-forwarding for service '{config.Service}'.");
            return (PortForwardResult.PermanentFailure, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting port-forwarding for service '{config.Service}': {ex.Message}");
            return (PortForwardResult.PermanentFailure, null);
        }
    }

    static async Task WaitForDisconnectionOrCancellation(Process kubectlProcess, CancellationToken cancellationToken)
    {
        var taskCompletionSource = new TaskCompletionSource<bool>();

        // Attach an event handler to the process's Exited event.
        kubectlProcess.Exited += (_, _) =>
        {
            taskCompletionSource.TrySetResult(true);
        };

        // This task will complete if the cancellation token is triggered.
        var cancellationTask = Task.Delay(-1, cancellationToken); 

        // Wait for either the process to exit or the cancellation token to be signaled.
        if (await Task.WhenAny(taskCompletionSource.Task, cancellationTask) == cancellationTask)
        {
            // If the cancellation token was signaled, kill the process.
            if (!kubectlProcess.HasExited)
            {
                kubectlProcess.Kill();
            }
        }
    }

    public enum PortForwardResult
    {
        Success,
        RetryableFailure,
        PermanentFailure
    }
}