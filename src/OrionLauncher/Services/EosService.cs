using System.IO;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using Epic.OnlineServices.Platform;
using Epic.OnlineServices.Sessions;
using OrionLauncher.Models;

namespace OrionLauncher.Services;

public class EosService : IDisposable
{
    private const string ProductId    = "108bae92517548518cbd371722381ded";
    private const string SandboxId    = "9c46d97dbe664f63823d11cf0b1cd8ae";
    private const string DeploymentId = "6db6bea492f94b1bbdfcdfe3e4f898dc";
    private const string ClientId     = "xyza7891hFoisXeQRt7s4NempeFlsPYC";
    private const string ClientSecret = "6guJP9tB4jkvQEGGg0KAkKl/j7Wa7bpLyQdczhV1FfQ";

    private PlatformInterface?   _platform;
    private ProductUserId?       _localUser;
    private System.Timers.Timer? _tickTimer;
    private readonly SemaphoreSlim _searchLock = new(1, 1);

    public bool IsConnected => _localUser?.IsValid() == true;

    public async Task InitializeAsync()
    {
        var initOpts = new InitializeOptions
        {
            ProductName    = "OrionLauncher",
            ProductVersion = "1.0"
        };
        var initResult = PlatformInterface.Initialize(ref initOpts);
        if (initResult != Result.Success && initResult != Result.AlreadyConfigured)
            throw new Exception($"EOS init failed: {initResult}");

        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OrionLauncher", "EOS");
        Directory.CreateDirectory(cacheDir);

        var opts = new Options
        {
            ProductId         = ProductId,
            SandboxId         = SandboxId,
            DeploymentId      = DeploymentId,
            ClientCredentials = new ClientCredentials { ClientId = ClientId, ClientSecret = ClientSecret },
            IsServer          = false,
            Flags             = PlatformFlags.DisableOverlay | PlatformFlags.DisableSocialOverlay,
            CacheDirectory    = cacheDir
        };
        _platform = PlatformInterface.Create(ref opts)
            ?? throw new Exception("EOS platform creation returned null");

        _tickTimer          = new System.Timers.Timer(100);
        _tickTimer.Elapsed += (_, _) => _platform?.Tick();
        _tickTimer.Start();

        await Task.CompletedTask;
    }

    public async Task ConnectAsync()
    {
        if (_platform == null) throw new InvalidOperationException("Call InitializeAsync first");
        _localUser = await LoginWithDeviceIdAsync(_platform.GetConnectInterface());
    }

    private static Task<ProductUserId> LoginWithDeviceIdAsync(ConnectInterface connect)
    {
        var tcs = new TaskCompletionSource<ProductUserId>(TaskCreationOptions.RunContinuationsAsynchronously);

        var loginOpts = new LoginOptions
        {
            Credentials   = new Credentials { Type = ExternalCredentialType.DeviceidAccessToken, Token = null },
            UserLoginInfo = new UserLoginInfo { DisplayName = "OrionPlayer" }
        };

        connect.Login(ref loginOpts, null, (ref LoginCallbackInfo info) =>
        {
            var result = info.ResultCode;
            var userId = info.LocalUserId;

            if (result == Result.Success)
            {
                tcs.TrySetResult(userId);
                return;
            }

            if (result == Result.InvalidUser)
            {
                var createOpts = new CreateDeviceIdOptions { DeviceModel = "PC" };
                connect.CreateDeviceId(ref createOpts, null, (ref CreateDeviceIdCallbackInfo ci) =>
                {
                    var ciResult = ci.ResultCode;
                    if (ciResult != Result.Success && ciResult != Result.DuplicateNotAllowed)
                    {
                        tcs.TrySetException(new Exception($"CreateDeviceId: {ciResult}"));
                        return;
                    }
                    var retryOpts = loginOpts;
                    connect.Login(ref retryOpts, null, (ref LoginCallbackInfo retry) =>
                    {
                        var retryResult = retry.ResultCode;
                        var retryUser   = retry.LocalUserId;
                        if (retryResult == Result.Success)
                            tcs.TrySetResult(retryUser);
                        else
                            tcs.TrySetException(new Exception($"EOS login retry: {retryResult}"));
                    });
                });
                return;
            }

            tcs.TrySetException(new Exception($"EOS login: {result}"));
        });

        return tcs.Task;
    }

    public async Task<List<ServerInfo>> SearchSessionsAsync()
    {
        if (_platform == null || _localUser == null)
            throw new InvalidOperationException("Not connected to EOS");

        await _searchLock.WaitAsync();
        try
        {
            return await RunSearchAsync(_platform.GetSessionsInterface());
        }
        finally
        {
            _searchLock.Release();
        }
    }

    private Task<List<ServerInfo>> RunSearchAsync(SessionsInterface sessions)
    {
        var tcs = new TaskCompletionSource<List<ServerInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var createOpts = new CreateSessionSearchOptions { MaxSearchResults = 200 };
        if (sessions.CreateSessionSearch(ref createOpts, out var search) != Result.Success || search == null)
        {
            tcs.TrySetException(new Exception("CreateSessionSearch failed"));
            return tcs.Task;
        }

        var findOpts = new SessionSearchFindOptions { LocalUserId = _localUser };
        search.Find(ref findOpts, null, (ref SessionSearchFindCallbackInfo info) =>
        {
            var findResult = info.ResultCode;
            if (findResult != Result.Success)
            {
                search.Release();
                tcs.TrySetException(new Exception($"SessionSearch.Find: {findResult}"));
                return;
            }

            try   { tcs.TrySetResult(ReadResults(search)); }
            finally { search.Release(); }
        });

        return tcs.Task;
    }

    private static List<ServerInfo> ReadResults(SessionSearch search)
    {
        var list      = new List<ServerInfo>();
        var countOpts = new SessionSearchGetSearchResultCountOptions();
        var count     = search.GetSearchResultCount(ref countOpts);

        for (uint i = 0; i < count; i++)
        {
            var copyOpts = new SessionSearchCopySearchResultByIndexOptions { SessionIndex = i };
            if (search.CopySearchResultByIndex(ref copyOpts, out var details) != Result.Success || details == null)
                continue;

            try
            {
                var infoOpts = new SessionDetailsCopyInfoOptions();
                if (details.CopyInfo(ref infoOpts, out var si) != Result.Success || si == null)
                    continue;

                var info = si.Value;
                var max     = (int)(info.Settings?.NumPublicConnections ?? 0);
                var open    = (int)info.NumOpenPublicConnections;
                var server  = new ServerInfo
                {
                    HostAddress = info.HostAddress?.ToString() ?? "",
                    MaxPlayers  = max,
                    PlayerCount = Math.Max(0, max - open)
                };

                ReadAttributes(details, server);
                list.Add(server);
            }
            finally
            {
                details.Release();
            }
        }

        return list;
    }

    private static void ReadAttributes(SessionDetails details, ServerInfo server)
    {
        var countOpts = new SessionDetailsGetSessionAttributeCountOptions();
        var count     = details.GetSessionAttributeCount(ref countOpts);

        for (uint j = 0; j < count; j++)
        {
            var attrOpts = new SessionDetailsCopySessionAttributeByIndexOptions { AttrIndex = j };
            if (details.CopySessionAttributeByIndex(ref attrOpts, out var attr) != Result.Success || attr == null)
                continue;

            var data = attr.Value.Data;
            if (data == null) continue;

            var key = data.Value.Key?.ToString() ?? "";
            var val = GetAttrString(data.Value.Value);
            if (string.IsNullOrEmpty(key)) continue;

            server.Attributes[key] = val;

            var keyUp = key.ToUpperInvariant();
            if (keyUp is "SERVERNAME" or "SERVER_NAME" or "NAME" or "SESSION_NAME")
                server.Name = val;
            else if (keyUp is "MAPNAME" or "MAP" or "MAP_NAME")
                server.MapName = val;
        }
    }

    private static string GetAttrString(AttributeDataValue v) =>
        v.ValueType switch
        {
            AttributeType.String  => v.AsUtf8?.ToString() ?? "",
            AttributeType.Int64   => v.AsInt64?.ToString() ?? "",
            AttributeType.Double  => v.AsDouble?.ToString() ?? "",
            AttributeType.Boolean => v.AsBool?.ToString() ?? "",
            _                     => ""
        };

    public void Dispose()
    {
        _tickTimer?.Stop();
        _tickTimer?.Dispose();
        _platform?.Release();
        _platform = null;
    }
}
