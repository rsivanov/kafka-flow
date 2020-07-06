﻿namespace KafkaFlow.IntegrationTests.NetFw.Core.Messages
{
    using System;

    public interface ITestMessage
    {
        Guid Id { get; set; }

        string Value { get; set; }

        int Version { get; set; }
    }
}
