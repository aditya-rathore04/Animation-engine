using System.Net;
using System.Text.Json;

namespace AnimationEngine;

public class TriggerServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly Action<TriggerRequest> _onTriggerReceived;
    private bool _running;
    private Task? _listenerTask;

    public TriggerServer(Action<TriggerRequest> onTriggerReceived)
    {
        _onTriggerReceived = onTriggerReceived ?? throw new ArgumentNullException(nameof(onTriggerReceived));
        _listener = new HttpListener();
        _listener.Prefixes.Add("http://127.0.0.1:5057/");
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _listener.Start();
        _listenerTask = Task.Run(ListenLoopAsync);
        Console.WriteLine("Server started on http://127.0.0.1:5057/");
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        try
        {
            _listener.Stop();
        }
        catch (ObjectDisposedException) { }
        
        // Wait for listener task to finish or cancel
        _listenerTask?.Wait(2000);
    }

    private async Task ListenLoopAsync()
    {
        while (_running)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                // Process each request asynchronously so we don't block the loop
                _ = Task.Run(() => HandleRequestAsync(context));
            }
            catch (Exception ex)
            {
                if (!_running) break;
                Console.WriteLine($"Listener loop error: {ex.Message}");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            var rawPath = request.Url?.AbsolutePath;
            var method = request.HttpMethod;

            if (method == "GET" && rawPath == "/ping")
            {
                await WriteResponseAsync(response, HttpStatusCode.OK, "ok");
                return;
            }

            if (method == "POST" && rawPath == "/trigger")
            {
                if (!request.HasEntityBody)
                {
                    await WriteResponseAsync(response, HttpStatusCode.BadRequest, "Missing request body");
                    return;
                }

                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                var requestBody = await reader.ReadToEndAsync();

                TriggerRequest? triggerRequest;
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    triggerRequest = JsonSerializer.Deserialize<TriggerRequest>(requestBody, options);
                }
                catch (JsonException ex)
                {
                    await WriteResponseAsync(response, HttpStatusCode.BadRequest, $"Malformed JSON: {ex.Message}");
                    return;
                }

                if (triggerRequest == null || string.IsNullOrWhiteSpace(triggerRequest.Message))
                {
                    await WriteResponseAsync(response, HttpStatusCode.BadRequest, "Message field is required");
                    return;
                }

                // Success response first to not block the caller
                await WriteResponseAsync(response, HttpStatusCode.OK, "triggered");

                // Invoke callback
                try
                {
                    _onTriggerReceived(triggerRequest);
                }
                catch (Exception callbackEx)
                {
                    Console.WriteLine($"Error in trigger callback: {callbackEx.Message}");
                }
                return;
            }

            // Path not found
            await WriteResponseAsync(response, HttpStatusCode.NotFound, "Not Found");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling request: {ex.Message}");
            try
            {
                await WriteResponseAsync(response, HttpStatusCode.InternalServerError, "Internal Server Error");
            }
            catch { }
        }
        finally
        {
            try
            {
                response.Close();
            }
            catch { }
        }
    }

    private async Task WriteResponseAsync(HttpListenerResponse response, HttpStatusCode statusCode, string content)
    {
        response.StatusCode = (int)statusCode;
        response.ContentType = "text/plain";
        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(content);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.OutputStream.Flush();
    }

    public void Dispose()
    {
        Stop();
        ((IDisposable)_listener).Dispose();
    }
}
