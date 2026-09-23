using NetworkStats.Tests;

(string Name, Func<Task> Run)[] tests =
[
    ("直连显式绕过系统代理", ProbeTests.DirectBypassesSystemProxyAsync),
    ("绿黄红判定、重定向和等待响应体", ProbeTests.ColorsRedirectAndBodyAsync),
    ("浏览器验证、访问拒绝与限流不生成虚假速度", HttpFailureTests.RejectionsAndChallengesAsync),
    ("配置 URL 的字节计量、总耗时与慢速判色", UrlProbeTests.ExactUrlAndBodyTimingAsync),
    ("响应等待独立计时、响应体下载速度和小样本标记", TransferTimingTests.SeparateWaitingAndDownloadingAsync),
    ("URL 自动采样的流量时限、空响应与断流", UrlProbeTests.LimitsAndFailuresAsync),
    ("访问速度历史恢复及旧版记录兼容", UrlProbeTests.HistoryCompatibilityAsync),
    ("超时与主动取消分别处理", ProbeTests.TimeoutAndCancellationAsync),
    ("通过指定 HTTP 代理访问", ProbeTests.HttpProxyAsync),
    ("SOCKS5 握手及代理端域名解析", ProbeTests.SocksProxyAsync),
    ("配置校验、规范化与持久化", StorageTests.ValidateAndPersistSettingsAsync),
    ("主题、关闭行为持久化及 ChatGPT 默认配置", PreferenceTests.DefaultsAndPersistenceAsync),
    ("分钟去重、历史保留、断行恢复和空白分钟", StorageTests.HistoryBucketsAndRecoveryAsync),
    ("并发限制、手动探测、暂停恢复及配置生效", EngineTests.SchedulingAndConcurrencyAsync),
    ("直连与多个代理的结果互不影响", EngineTests.RoutesRemainIndependentAsync),
    ("暂停时不生成误报", EngineTests.PauseDoesNotCreateRedSamplesAsync),
    ("实际下载字节、过程进度和直连绕过系统代理", DownloadSpeedTests.StreamingAndDirectAsync),
    ("下载流量限制、全程时限和主动取消", DownloadSpeedTests.LimitsAndCancellationAsync),
    ("下载重定向、错误、断流和压缩数据计量", DownloadSpeedTests.RedirectErrorsAndEncodingAsync),
    ("通过 HTTP 和 SOCKS5 代理下载测速", DownloadSpeedTests.ProxyRoutesAsync)
];
var failed = 0;
foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception exception) { failed++; Console.Error.WriteLine($"FAIL {test.Name}\n{exception}"); }
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} checks passed");
return failed == 0 ? 0 : 1;
