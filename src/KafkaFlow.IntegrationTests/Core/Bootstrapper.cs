namespace KafkaFlow.IntegrationTests.Core
{
    using System;
    using System.IO;
    using System.Threading;
    using global::Microsoft.Extensions.Configuration;
    using global::Microsoft.Extensions.DependencyInjection;
    using global::Microsoft.Extensions.Hosting;
    using KafkaFlow.Compressor;
    using KafkaFlow.Compressor.Gzip;
    using KafkaFlow.IntegrationTests.Core.Handlers;
    using KafkaFlow.IntegrationTests.Core.Middlewares;
    using KafkaFlow.IntegrationTests.Core.Middlewares.Producers;
    using KafkaFlow.IntegrationTests.Core.TypeResolvers;
    using KafkaFlow.Serializer;
    using KafkaFlow.Serializer.Json;
    using KafkaFlow.Serializer.ProtoBuf;
    using KafkaFlow.TypedHandler;

    public class Bootstrapper
    {
        private const string ProtobufTopicName = "topic-protobuf-netcore";
        private const string JsonTopicName = "topic-json-netcore";
        private const string GzipTopicName = "topic-gzip-netcore";
        private const string JsonGzipTopicName = "topic-json-gzip-netcore";
        private const string ProtobufGzipTopicName = "topic-protobuf-gzip-netcore";
        private const string ProtobufGzipTopicName2 = "topic-protobuf-gzip-2-netcore";

        private const string ProtobufConsumerId = "consumer-protobuf-netcore";
        private const string JsonConsumerId = "consumer-protobuf-netcore";
        private const string GzipConsumerId = "consumer-gzip-netcore";
        private const string JsonGzipConsumerId = "consumer-json-gzip-netcore";
        private const string ProtobufGzipConsumerId = "consumer-protobuf-gzip-netcore";


        private static readonly Lazy<IServiceProvider> lazyProvider = new Lazy<IServiceProvider>(SetupProvider);

        public static IServiceProvider GetServiceProvider() => lazyProvider.Value;

        private static IServiceProvider SetupProvider()
        {
            var builder = Host
                .CreateDefaultBuilder()
                .ConfigureAppConfiguration(
                    (builderContext, config) =>
                    {
                        config
                            .SetBasePath(Directory.GetCurrentDirectory())
                            .AddJsonFile(
                                "conf/appsettings.json",
                                false,
                                true)
                            .AddEnvironmentVariables();
                    })
                .ConfigureServices(SetupServices)
                .UseDefaultServiceProvider(
                    (context, options) =>
                    {
                        options.ValidateScopes = true;
                        options.ValidateOnBuild = true;
                    });

            var host = builder.Build();
            var bus = host.Services.CreateKafkaBus();
            bus.StartAsync().GetAwaiter().GetResult();
            //Wait partition assignment
            Thread.Sleep(10000);

            return host.Services;
        }

        private static void SetupServices(HostBuilderContext context, IServiceCollection services)
        {
            var brokers = context.Configuration.GetValue<string>("Kafka:Brokers");

            services.AddKafka(
                kafka => kafka
                    .UseLogHandler<TraceLoghandler>()
                    .AddCluster(
                        cluster => cluster
                            .WithBrokers(brokers.Split(';'))
                            .AddConsumer(
                                consumer => consumer
                                    .Topic(ProtobufTopicName)
                                    .WithGroupId(ProtobufConsumerId)
                                    .WithBufferSize(100)
                                    .WithWorkersCount(10)
                                    .WithAutoOffsetReset(AutoOffsetReset.Latest)
                                    .AddMiddlewares(
                                        middlewares => middlewares
                                            .AddSerializer<ProtobufMessageSerializer, SampleMessageTypeResolver>()
                                            .AddTypedHandlers(
                                                handlers =>
                                                    handlers
                                                        .WithHandlerLifetime(InstanceLifetime.Singleton)
                                                        .AddHandler<MessageHandler>())
                                    )
                            )
                            .AddConsumer(
                                consumer => consumer
                                    .Topic(JsonTopicName)
                                    .WithGroupId(JsonConsumerId)
                                    .WithBufferSize(100)
                                    .WithWorkersCount(10)
                                    .WithAutoOffsetReset(AutoOffsetReset.Latest)
                                    .AddMiddlewares(
                                        middlewares => middlewares
                                            .AddSerializer<JsonMessageSerializer, SampleMessageTypeResolver>()
                                            .AddTypedHandlers(
                                                handlers =>
                                                    handlers
                                                        .WithHandlerLifetime(InstanceLifetime.Singleton)
                                                        .AddHandlersFromAssemblyOf<MessageHandler>())
                                    )
                            )
                            .AddConsumer(
                                consumer => consumer
                                    .Topics(GzipTopicName)
                                    .WithGroupId(GzipConsumerId)
                                    .WithBufferSize(100)
                                    .WithWorkersCount(10)
                                    .WithAutoOffsetReset(AutoOffsetReset.Latest)
                                    .AddMiddlewares(
                                        middlewares => middlewares
                                            .AddCompressor<GzipMessageCompressor>()
                                            .Add<GzipMiddleware>()
                                    )
                            )
                            .AddConsumer(
                                consumer => consumer
                                    .Topics(JsonGzipTopicName)
                                    .WithGroupId(JsonGzipConsumerId)
                                    .WithBufferSize(100)
                                    .WithWorkersCount(10)
                                    .WithAutoOffsetReset(AutoOffsetReset.Latest)
                                    .AddMiddlewares(
                                        middlewares => middlewares
                                            .AddCompressor<GzipMessageCompressor>()
                                            .AddSerializer<JsonMessageSerializer, SampleMessageTypeResolver>()
                                            .AddTypedHandlers(
                                                handlers =>
                                                    handlers
                                                        .WithHandlerLifetime(InstanceLifetime.Singleton)
                                                        .AddHandler<MessageHandler>())
                                    )
                            )
                            .AddConsumer(
                                consumer => consumer
                                    .Topics(ProtobufGzipTopicName, ProtobufGzipTopicName2)
                                    .WithGroupId(ProtobufGzipConsumerId)
                                    .WithBufferSize(100)
                                    .WithWorkersCount(10)
                                    .WithAutoOffsetReset(AutoOffsetReset.Latest)
                                    .WithAutoCommitIntervalMs(1)
                                    .AddMiddlewares(
                                        middlewares => middlewares
                                            .AddCompressor<GzipMessageCompressor>()
                                            .AddSerializer<ProtobufMessageSerializer, SampleMessageTypeResolver>()
                                            .AddTypedHandlers(
                                                handlers =>
                                                    handlers
                                                        .WithHandlerLifetime(InstanceLifetime.Singleton)
                                                        .AddHandler<MessageHandler>())
                                    )
                            )
                            .AddProducer<JsonProducer>(
                                producer => producer
                                    .DefaultTopic(JsonTopicName)
                                    .AddMiddlewares(
                                        middlewares => middlewares
                                            .AddSerializer<JsonMessageSerializer, SampleMessageTypeResolver>()
                                    )
                            )
                            .AddProducer<JsonGzipProducer>(
                                producer => producer
                                    .DefaultTopic(JsonGzipTopicName)
                                    .AddMiddlewares(
                                        middlewares => middlewares
                                            .AddSerializer<JsonMessageSerializer, SampleMessageTypeResolver>()
                                            .AddCompressor<GzipMessageCompressor>()
                                    )
                            )
                            .AddProducer<ProtobufProducer>(
                                producer => producer
                                    .DefaultTopic(ProtobufTopicName)
                                    .AddMiddlewares(
                                        middlewares => middlewares
                                            .AddSerializer<ProtobufMessageSerializer, SampleMessageTypeResolver>()
                                    )
                            )
                            .AddProducer<ProtobufGzipProducer>(
                                producer => producer
                                    .DefaultTopic(ProtobufGzipTopicName)
                                    .AddMiddlewares(
                                        middlewares => middlewares
                                            .AddSerializer<ProtobufMessageSerializer, SampleMessageTypeResolver>()
                                            .AddCompressor<GzipMessageCompressor>()
                                    )
                            )
                            .AddProducer<ProtobufGzipProducer2>(
                                producer => producer
                                    .DefaultTopic(ProtobufGzipTopicName2)
                                    .AddMiddlewares(
                                        middlewares => middlewares
                                            .AddSerializer<ProtobufMessageSerializer, SampleMessageTypeResolver>()
                                            .AddCompressor<GzipMessageCompressor>()
                                    )
                            )
                            .AddProducer<GzipProducer>(
                                producer => producer
                                    .DefaultTopic(GzipTopicName)
                                    .AddMiddlewares(
                                        middlewares => middlewares
                                            .AddCompressor<GzipMessageCompressor>()
                                    )
                            )
                    )
            );

            services.AddSingleton<JsonProducer>();
            services.AddSingleton<JsonGzipProducer>();
            services.AddSingleton<ProtobufProducer>();
            services.AddSingleton<ProtobufGzipProducer2>();
            services.AddSingleton<ProtobufGzipProducer>();
            services.AddSingleton<GzipProducer>();
        }
    }
}
