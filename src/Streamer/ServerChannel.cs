using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Streamer
{
    public class ServerChannel
    {
        private readonly Dictionary<string, Func<Request, Response>> _callbacks = new Dictionary<string, Func<Request, Response>>(StringComparer.OrdinalIgnoreCase);

        private bool _isBound;

        private readonly JsonSerializer _serializer;

        public ServerChannel()
        {
            _serializer = new JsonSerializer();
        }

        public IDisposable Bind(object value)
        {
            if (_isBound)
            {
                throw new NotSupportedException("Can't bind to different objects");
            }

            _isBound = true;

            var methods = new List<string>();

            var type = value.GetType();
            foreach (var m in type.GetTypeInfo().DeclaredMethods.Where(m => m.IsPublic))
            {
                var methodName = type.FullName + "." + m.Name;

                methods.Add(methodName);

                var parameters = m.GetParameters();

                if (_callbacks.ContainsKey(methodName))
                {
                    throw new NotSupportedException(String.Format("Duplicate definitions of {0}. Overloading is not supported.", m.Name));
                }

                _callbacks[methodName] = request =>
                {
                    var response = new Response();
                    response.Id = request.Id;

                    try
                    {
                        var args = request.Args.Zip(parameters, (a, p) => a.ToObject(p.ParameterType))
                                               .ToArray();

                        var result = m.Invoke(value, args);

                        if (result != null)
                        {
                            response.Result = JToken.FromObject(result);
                        }
                    }
                    catch (TargetInvocationException ex)
                    {
                        response.Error = ex.InnerException.Message;
                    }
                    catch (Exception ex)
                    {
                        response.Error = ex.Message;
                    }

                    return response;
                };
            }

            return new DisposableAction(() =>
            {
                foreach (var m in methods)
                {
                    lock (_callbacks)
                    {
                        _callbacks.Remove(m);
                    }
                }
            });
        }

        public async Task StartAsync(Stream stream)
        {
            try
            {
                while (true)
                {
                    // REVIEW: This does a blocking read
                    var reader = new JsonTextReader(new StreamReader(stream));
                    var request = _serializer.Deserialize<Request>(reader);

                    Response response = null;

                    Func<Request, Response> callback;
                    if (_callbacks.TryGetValue(request.Method, out callback))
                    {
                        response = callback(request);
                    }
                    else
                    {
                        // If there's no method then return a failed response for this request
                        response = new Response
                        {
                            Id = request.Id,
                            Error = string.Format("Unknown method '{0}'", request.Method)
                        };
                    }

                    await WriteAsync(stream, response);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private Task WriteAsync(Stream stream, object value)
        {
            var data = JsonConvert.SerializeObject(value);

            var bytes = Encoding.UTF8.GetBytes(data);

            return stream.WriteAsync(bytes, 0, bytes.Length);
        }

        private class DisposableAction : IDisposable
        {
            private Action _action;

            public DisposableAction(Action action)
            {
                _action = action;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _action, () => { }).Invoke();
            }
        }
    }
}
