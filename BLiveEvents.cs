﻿using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Google.Protobuf;
using Newtonsoft.Json.Linq;

namespace BLiveAPI;

/// <summary>
///     BLiveAPI的各种事件
/// </summary>
public abstract class BLiveEvents
{
    /// <inheritdoc />
    public delegate void BLiveEventHandler<in TEventArgs>(object sender, TEventArgs e);

    /// <inheritdoc cref="BLiveEvents" />
    protected BLiveEvents()
    {
        SendSmsReply += OnDanmuMsg;
    }

    /// <summary>
    ///     服务器回复的认证消息
    /// </summary>
    public event BLiveEventHandler<(JObject authReply, byte[] rawData)> OpAuthReply;

    /// <inheritdoc cref="OpAuthReply" />
    protected void OnOpAuthReply(JObject authReply, byte[] rawData)
    {
        OpAuthReply?.Invoke(this, (authReply, rawData));
    }

    /// <summary>
    ///     服务器回复的心跳消息
    /// </summary>
    public event BLiveEventHandler<(int heartbeatReply, byte[] rawData)> OpHeartbeatReply;

    /// <inheritdoc cref="OpHeartbeatReply" />
    protected void OnOpHeartbeatReply(int heartbeatReply, byte[] rawData)
    {
        OpHeartbeatReply?.Invoke(this, (heartbeatReply, rawData));
    }

    /// <summary>
    ///     服务器发送的SMS消息
    /// </summary>
    public event BLiveEventHandler<(string cmd, string hitCmd, JObject rawData)> OpSendSmsReply;

    private void InvokeOpSendSmsReply(JObject rawData, bool hit)
    {
        if (OpSendSmsReply is null) return;
        var waitInvokeList = OpSendSmsReply.GetInvocationList().ToList();
        var cmd = (string)rawData["cmd"];
        foreach (var invocation in OpSendSmsReply.GetInvocationList())
        {
            var targetCmdAttribute = invocation.Method.GetCustomAttributes<TargetCmdAttribute>().FirstOrDefault();
            if (targetCmdAttribute is null)
            {
                invocation.DynamicInvoke(this, (cmd, "ALL", rawData));
                waitInvokeList.Remove(invocation);
            }
            else if (targetCmdAttribute.HasCmd(cmd))
            {
                invocation.DynamicInvoke(this, (cmd, cmd, rawData));
                waitInvokeList.Remove(invocation);
                hit = true;
            }
            else if (targetCmdAttribute.HasCmd("ALL"))
            {
                invocation.DynamicInvoke(this, (cmd, "ALL", rawData));
                waitInvokeList.Remove(invocation);
            }
            else if (!targetCmdAttribute.HasCmd("OTHERS"))
            {
                waitInvokeList.Remove(invocation);
            }
        }

        if (hit) return;
        foreach (var invocation in waitInvokeList) invocation.DynamicInvoke(this, (cmd, "OTHERS", rawData));
    }

    private event BLiveSmsEventHandler SendSmsReply;

    private bool InvokeSendSmsReply(JObject rawData)
    {
        if (SendSmsReply is null) return false;
        var cmd = (string)rawData["cmd"];
        return (from invocation in SendSmsReply.GetInvocationList()
            let targetCmdAttribute = invocation.Method.GetCustomAttributes<TargetCmdAttribute>().FirstOrDefault()
            where targetCmdAttribute != null && targetCmdAttribute.HasCmd(cmd)
            select invocation).Aggregate(false, (current, invocation) => (bool)invocation.DynamicInvoke(rawData) || current);
    }

    /// <inheritdoc cref="OpSendSmsReply" />
    protected void OnOpSendSmsReply(JObject rawData)
    {
        InvokeOpSendSmsReply(rawData, InvokeSendSmsReply(rawData));
    }

    /// <summary>
    ///     弹幕消息
    /// </summary>
    public event BLiveEventHandler<(string msg, long userId, string userName, string face, JObject rawData)> DanmuMsg;

    private static byte[] GetChildFromProtoData(byte[] protoData, int target)
    {
        using (var input = new CodedInputStream(protoData))
        {
            while (!input.IsAtEnd)
            {
                var tag = input.ReadTag();
                var tagId = WireFormat.GetTagFieldNumber(tag);
                if (tagId == target) return input.ReadBytes().ToByteArray();
                input.SkipLastField();
            }
        }

        return Array.Empty<byte>();
    }

    /// <inheritdoc cref="DanmuMsg" />
    [TargetCmd("DANMU_MSG")]
    private bool OnDanmuMsg(JObject rawData)
    {
        var msg = (string)rawData["info"][1];
        var userId = (long)rawData["info"][2]?[0];
        var userName = (string)rawData["info"][2]?[1];
        var protoData = Convert.FromBase64String(rawData["dm_v2"].ToString());
        var face = Encoding.UTF8.GetString(GetChildFromProtoData(GetChildFromProtoData(protoData, 20), 4));
        DanmuMsg?.Invoke(this, (msg, userId, userName, face, rawData));
        return DanmuMsg is not null;
    }

    /// <summary>
    ///     WebSocket异常关闭
    /// </summary>
    public event BLiveEventHandler<(string message, int code)> WebSocketError;

    /// <inheritdoc cref="WebSocketError" />
    protected void OnWebSocketError(string message, int code)
    {
        WebSocketError?.Invoke(this, (message, code));
    }

    /// <summary>
    ///     WebSocket主动关闭
    /// </summary>
    public event BLiveEventHandler<(string message, int code)> WebSocketClose;

    /// <inheritdoc cref="WebSocketClose" />
    protected void OnWebSocketClose(string message, int code)
    {
        WebSocketClose?.Invoke(this, (message, code));
    }

    /// <summary>
    ///     解析消息过程出现的错误，不影响WebSocket正常运行，所以不抛出异常
    /// </summary>
    public event BLiveEventHandler<(string message, Exception e)> DecodeError;

    /// <inheritdoc cref="DecodeError" />
    protected void OnDecodeError(string message, Exception e)
    {
        DecodeError?.Invoke(this, (message, e));
    }

    private delegate bool BLiveSmsEventHandler(JObject rawData);
}