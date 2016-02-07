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
    public class ServerChannel : IDisposable
    {
        private readonly Stream _stream;

        private readonly Dictionary<string, Func<Request, Response>> _callbacks = new Dictionary<string, Func<Request, Response>>(StringComparer.OrdinalIgnoreCase);

        private bool _isBound;

        public ServerChannel(Stream stream)
        {
            _stream = stream;

            // REVIEW: Thread per connection is bad :), use an async read loop
            new Thread(() => ReadLoop()).Start();
        }

        public IDisposable Bind(object value)
        {
            if (_isBound)
            {
                throw new NotSupportedException("Can't bind to different objects");
            }

            _isBound = true;

            var methods = new List<string>();

            foreach (var m in value.GetType().GetTypeInfo().DeclaredMethods.Where(m => m.IsPublic))
            {
                methods.Add(m.Name);

                var parameters = m.GetParameters();

                if (_callbacks.ContainsKey(m.Name))
                {
                    throw new NotSupportedException(String.Format("Duplicate definitions of {0}. Overloading is not supported.", m.Name));
                }

                _callbacks[m.Name] = request =>
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

        private void ReadLoop()
        {
            try
            {
                var serializer = new JsonSerializer();
                while (true)
                {
                    var reader = new JsonTextReader(new StreamReader(_stream));


                    var request = serializer.Deserialize<Request>(reader);

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

                    Write(response);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void Write(object value)
        {
            var data = JsonConvert.SerializeObject(value);

            var bytes = Encoding.UTF8.GetBytes(data);

            _stream.Write(bytes, 0, bytes.Length);
        }

        public void Dispose()
        {
            _stream.Dispose();
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
