using NetworkStats.Tests;

(string Name, Func<Task> Run)[] tests =
[
    ("直连显式绕过系统代理", ProbeTests.DirectBypassesSystemProxyAsync),
    ("绿黄红判定、重定向和仅等待响应头", ProbeTests.ColorsRedirectAndHeadersAsync),
    ("超时与主动取消分别处理", ProbeTests.TimeoutAndCancellationAsync),
    ("通过指定 HTTP 代理访问", ProbeTests.HttpProxyAsync),
    ("SOCKS5 握手及代理端域名解析", ProbeTests.SocksProxyAsync),
    ("配置校验、规范化与持久化", StorageTests.ValidateAndPersistSettingsAsync),
    ("分钟去重、历史保留、断行恢复和空白分钟", StorageTests.HistoryBucketsAndRecoveryAsync),
    ("并发限制、手动探测、暂停恢复及配置生效", EngineTests.SchedulingAndConcurrencyAsync),
    ("直连与多个代理的结果互不影响", EngineTests.RoutesRemainIndependentAsync),
    ("暂停时不生成误报", EngineTests.PauseDoesNotCreateRedSamplesAsync)
];
var failed = 0;
foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception exception) { failed++; Console.Error.WriteLine($"FAIL {test.Name}\n{exception}"); }
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} checks passed");
return failed == 0 ? 0 : 1;
