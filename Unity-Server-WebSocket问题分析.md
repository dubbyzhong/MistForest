# Unity 与服务器 WebSocket 问题分析

本文记录 Unity 客户端连接服务器时出现的空消息类型，以及 Unity 暂停后服务器无法响应 Ctrl+C 退出的问题。本文是原因分析和建议修改点；源代码中没有保留这两项修复。

## 1. `unknown message type ''`

### 原因

Unity 网络驱动通过 Newtonsoft.Json 序列化消息，并使用 `JsonSettings.Default`。该设置包含 `CamelCaseNamingStrategy`，所以协议对象的 `Type` 成员会发成小写开头的 `type`，例如：

```json
{"type":"hello"}
```

服务器使用 `System.Text.Json`，当前 `JsonSerializerOptions` 是默认配置。默认配置区分 JSON 属性名大小写，因此 `type` 不会绑定到 `EnvelopeMsg.Type`，该属性保留默认空字符串。服务器将它当作未知类型并回传 `unknown message type ''`。Unity 收到 Reject 后，`BattleBootstrap.Update` 把错误写入 Console。

### 建议修改

让服务器读取协议属性时忽略属性名大小写：

```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    PropertyNameCaseInsensitive = true,
};
```

这样服务器可以接收 Unity 的 camelCase，也保留当前 SmokeClient 使用的 PascalCase。此选项影响反序列化；服务器现有的 PascalCase 输出不变。另一种做法是统一双方的 JSON 命名策略，但需要同步调整 SmokeClient 等其他客户端。

## 2. Ctrl+C 后停在 `Application is shutting down...`

### 原因

服务器接受 WebSocket 后，会在 `HandleClientAsync` 中持续等待下一条消息。原接收调用使用 `CancellationToken.None`：

```csharp
await socket.ReceiveAsync(new Memory<byte>(buffer), CancellationToken.None);
```

Unity 编辑器暂停 Play Mode 时，客户端连接不一定会关闭。Ctrl+C 会让 ASP.NET Core 主机开始关闭并输出 `Application is shutting down...`，但活动中的 WebSocket 请求仍卡在接收循环里；主机等待请求结束，因此进程迟迟不退出。该日志本身是正常的关闭提示，阻塞点是仍未结束的 WebSocket 请求。

### 建议修改

- 将 `context.RequestAborted` 与 `app.Lifetime.ApplicationStopping` 合并为连接取消令牌，并传给 `ReceiveAsync`。
- 捕获取消和 WebSocket 断开异常，在 `finally` 中移除并释放 socket。
- 主机停止时中断活动 socket，确保即使接收或发送正等待，也能唤醒连接处理器。
- 对客户端正常发来的 Close 帧回送关闭帧。

这样 Ctrl+C 仍会打印主机关闭日志，但活动 WebSocket 不再无限期阻止进程退出。

## 相关位置

- Unity JSON 命名设置：`MistForest-Unity/Assets/MistForestClient/Scripts/Utils/JsonSettings.cs`
- Unity WebSocket 收发：`MistForest-Unity/Assets/MistForestClient/Scripts/App/NetworkGameDriver.cs`
- Unity 错误输出：`MistForest-Unity/Assets/MistForestClient/Scripts/App/BattaleBootstrap.cs`
- 服务器 WebSocket 处理：`server/MistForest.Server/Program.cs`
