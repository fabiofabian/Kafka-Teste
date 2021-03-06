using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using KafkaDemo.Helpers;
using KafkaDemo.Helpers.Kafka;
using KafkaDemo.Inputs;
using KafkaDemo.Services;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace KafkaDemo
{
  public class Startup
  {
    public Startup(IConfiguration configuration)
    {
      Configuration = configuration;
    }

    public IConfiguration Configuration { get; }
    private AppSettings AppSettings { get; set; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
      var appSettings = new AppSettings();
      Configuration.Bind(appSettings);
      AppSettings = appSettings;

      services.AddSingleton<IKafkaProducer<Input>>(new KafkaProducer<Input>(appSettings.Kafka.BootstrapServers,
          appSettings.Kafka.Topic));
      services.AddSingleton<IKafkaProducer<KafkaRetry<Input>>>(new KafkaProducer<KafkaRetry<Input>>(appSettings.Kafka.BootstrapServers,
          appSettings.Kafka.ExceptionTopic));

      services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
      services.AddSwaggerGen(c =>
      {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
      });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IHostingEnvironment env)
    {
      ConfigureKafka(app);

      if (env.IsDevelopment())
      {
        app.UseDeveloperExceptionPage();
      }
      app.UseSwagger();
      app.UseSwaggerUI(c =>
      {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
      });

      app.UseRouting();

      app.UseEndpoints(endpoints =>
      {
        endpoints.MapControllerRoute(
                  name: "default",
                  pattern: "{controller}/{action=Index}/{id?}");
      });
    }

    public void ConfigureKafka(IApplicationBuilder app)
    {
      ProcessingService.Producer = app.ApplicationServices.GetService<IKafkaProducer<KafkaRetry<Input>>>();
      RetryProcessingService.Producer = app.ApplicationServices.GetService<IKafkaProducer<KafkaRetry<Input>>>();

      var kafkaConsumer =
          new KafkaConsumer<Input>(AppSettings.Kafka.BootstrapServers, AppSettings.Kafka.Topic, AppSettings.Kafka.GroupId);
      kafkaConsumer.Receive(ProcessingService.Handler);

      var exceptionConsumer =
          new KafkaConsumer<KafkaRetry<Input>>(AppSettings.Kafka.BootstrapServers,
              AppSettings.Kafka.ExceptionTopic, AppSettings.Kafka.ExceptionGroupId);
      exceptionConsumer.Receive(RetryProcessingService.Handler);
    }
  }
}
