using System.Collections.Concurrent;
using VRC.OSCQuery;

namespace Hai.HView.OSC;

public class HQuery
{
    public const string VrchatClientPrefix = "VRChat-Client";
    
    public event TargetOscPortFound OnTargetOscPortFound;
    public delegate void TargetOscPortFound(int oscPort);
    
    private readonly ConcurrentQueue<object> _queue = new ConcurrentQueue<object>();
    private readonly int _port;
    private readonly int _queryPort;
    private OSCQueryService _ourService;
    private OSCQueryServiceProfile _vrcQueryNullable;
    private readonly string _serviceName;
    private readonly List<string> _targetPrefixes;
    private bool _requiresVrSystem = false; // Stop asking for VR System (we don't need it)

    public static HQuery ForVrchat(int oscPort, int queryPort, string serviceName)
    {
        return new HQuery(oscPort, queryPort, serviceName, new[] { VrchatClientPrefix });
    }

    public HQuery(int oscPort, int queryPort, string serviceName, IEnumerable<string> allowedPrefixes)
    {
        _port = oscPort;
        _queryPort = queryPort;
        _serviceName = serviceName;
        _targetPrefixes = allowedPrefixes.ToList();
    }

    public static int RandomQueryPort()
    {
        return Extensions.GetAvailableTcpPort();
    }

    public void Start()
    {
        var qPort = _queryPort;
        _ourService = new OSCQueryServiceBuilder().WithServiceName(_serviceName)
            .WithTcpPort(qPort)
            .WithUdpPort(_port)
            .WithDiscovery(new MeaModDiscovery())
            .StartHttpServer()
            .AdvertiseOSC()
            .AdvertiseOSCQuery()
            .Build();
        _ourService.AddEndpoint(CommonOSCAddresses.AvatarChangeOscAddress, "s", Attributes.AccessValues.WriteOnly);
        // _ourService.AddEndpoint("/avatar/parameters/VelocityX", "f", Attributes.AccessValues.WriteOnly);
        if (_requiresVrSystem)
        {
            // Adding this endpoint causes VRC to send tracking data to us, which we don't need.
            _ourService.AddEndpoint("/tracking/vrsystem", "ffffff", Attributes.AccessValues.WriteOnly);
        }
        _ourService.OnOscQueryServiceAdded += profile =>
        {
            Console.WriteLine($"Found a query service at: {profile.name}");
            if (_targetPrefixes.Any(possiblePrefix => profile.name.StartsWith(possiblePrefix)))
            {
                _vrcQueryNullable = profile;
                Console.WriteLine($"Service Query is at http://{profile.address}:{profile.port}/");
                Task.Run(async () => await AsyncGetService(profile));
            }
        };
        _ourService.OnOscServiceAdded += profile =>
        {
            if (_targetPrefixes.Any(possiblePrefix => profile.name.StartsWith(possiblePrefix)))
            {
                Console.WriteLine($"Service OSC is at osc://{profile.address}:{profile.port}/");
                OnTargetOscPortFound?.Invoke(profile.port);
            }
        };
        _ourService.RefreshServices();
    }

    private async Task AsyncGetService(OSCQueryServiceProfile profile)
    {
        var tree = await Extensions.GetOSCTree(profile.address, profile.port);

        try
        {
            var all = Flatten(tree)
                .Where(node => node.Access != Attributes.AccessValues.NoValue)
                .ToArray();
            foreach (var parameter in all)
            {
                _queue.Enqueue(new HQueryMessageEvent
                {
                    Node = parameter
                });
            }
            _queue.Enqueue(new HQueryCompleteEvent());
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private IEnumerable<OSCQueryNode> Flatten(OSCQueryRootNode treeContents)
    {
        return Flatten2(treeContents.Contents.Values);
    }

    private IEnumerable<OSCQueryNode> Flatten2(Dictionary<string, OSCQueryNode>.ValueCollection nodeContents)
    {
        return nodeContents
            .Concat(nodeContents
                .Where(node => node.Contents != null && node.Contents.Values != null)
                .SelectMany(node => Flatten2(node.Contents.Values))
            );
    }

    public List<object> PullMessages()
    {
        var pulled = new List<object>();
        while (_queue.TryDequeue(out var obj))
        {
            pulled.Add(obj);
        }

        return pulled;
    }

    public void Refresh()
    {
        if (_vrcQueryNullable == null) return;
        
        AsyncGetService(_vrcQueryNullable);
    }

    public void Finish()
    {
        _ourService.Dispose();
    }

    public struct HQueryMessageEvent
    {
        public OSCQueryNode Node;
    }

    public struct HQueryCompleteEvent
    {
    }
}