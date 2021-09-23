﻿using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Models.JwtBearer;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Web.Filters;
using Web.Global;
using Web.Libraries.Swagger;
using Web.Permission.Action;
using Medallion.Threading;
using Medallion.Threading.SqlServer;

namespace Web.Extension
{
    public static class ServiceConfiguration
    {
        public static IServiceCollection ConfigServies(this IServiceCollection services, IConfiguration Configuration)
        {
            #region json配置
            //json动态响应压缩https://docs.microsoft.com/zh-cn/aspnet/core/performance/response-compression?view=aspnetcore-5.0
            services.AddResponseCompression();
            services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = long.MaxValue;
            });

            services.Configure<KestrelServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;
            });

            services.Configure<IISServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;
            });

            services.AddHsts(options =>
            {
                options.MaxAge = TimeSpan.FromDays(365);
            });
            #endregion


            //权限校验
            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireAssertion(context => IdentityVerification.Authorization(context)).Build();
            });

            services.AddControllers();

            #region jwt

            //注册JWT认证机制 之前的单机jwt 不适用 除非小项目 无单点登录情况下使用
            //services.Configure<JwtSettings>(Configuration.GetSection("JwtSettings"));
            //var jwtSettings = new JwtSettings();
            //Configuration.Bind("JwtSettings", jwtSettings);

            //services.AddAuthentication(options =>
            //{
            //    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            //    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            //})
            //.AddJwtBearer(o =>
            //{
            //    //主要是jwt  token参数设置
            //    o.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            //    {
            //        ValidIssuer = jwtSettings.Issuer,
            //        ValidAudience = jwtSettings.Audience,
            //        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            //        ValidateLifetime = false

            //        /***********************************TokenValidationParameters的参数默认值***********************************/
            //        // RequireSignedTokens = true,
            //        // SaveSigninToken = false,
            //        // ValidateActor = false,
            //        // 将下面两个参数设置为false，可以不验证Issuer和Audience，但是不建议这样做。
            //        // ValidateAudience = true,
            //        // ValidateIssuer = true, 
            //        // ValidateIssuerSigningKey = false,
            //        // 是否要求Token的Claims中必须包含Expires
            //        // RequireExpirationTime = true,
            //        // 允许的服务器时间偏移量
            //        // ClockSkew = TimeSpan.FromSeconds(300),
            //        // 是否验证Token有效期，使用当前时间与Token的Claims中的NotBefore和Expires对比
            //        // ValidateLifetime = true

            //    };
            //});

            #endregion

            //注册HttpContext
            Web.Libraries.Http.HttpContext.Add(services);

            //注册全局过滤器
            services.AddMvc(config => {
                config.Filters.Add(new GlobalFilter()); 
            });

            //注册跨域信息
            services.AddCors(option =>
            {
                option.AddPolicy("cors", policy =>
                {
                    policy.SetIsOriginAllowed(origin => true)
                       .AllowAnyHeader()
                       .AllowAnyMethod()
                       .AllowCredentials();
                });
            });


            services.AddControllers().AddJsonOptions(option =>
            {
                option.JsonSerializerOptions.Converters.Add(new Common.Json.DateTimeConverter());
                option.JsonSerializerOptions.Converters.Add(new Common.Json.DateTimeNullConverter());
                option.JsonSerializerOptions.Converters.Add(new Common.Json.LongConverter());
            });

            services.AddControllers().AddControllersAsServices(); //控制器当做实例创建

            //注册配置文件信息
            Web.Libraries.Start.StartConfiguration.Add(Configuration);

            #region Api版本以及配置
            services.AddApiVersioning(options =>
            {
                //通过Header向客户端通报支持的版本
                options.ReportApiVersions = true;

                //允许不加版本标记直接调用接口
                options.AssumeDefaultVersionWhenUnspecified = true;

                //接口默认版本
                //options.DefaultApiVersion = new ApiVersion(1, 0);

                //如果未加版本标记默认以当前最高版本进行处理
                options.ApiVersionSelector = new CurrentImplementationApiVersionSelector(options);

                options.ApiVersionReader = new HeaderApiVersionReader("api-version");
                //options.ApiVersionReader = new QueryStringApiVersionReader("api-version");
            });


            services.AddVersionedApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

            //注册统一模型验证
            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = actionContext =>
                {

                    //获取验证失败的模型字段 
                    var errors = actionContext.ModelState.Where(e => e.Value.Errors.Count > 0).Select(e => e.Value.Errors.First().ErrorMessage).ToList();

                    var dataStr = string.Join(" | ", errors);

                    //设置返回内容
                    var result = new
                    {
                        errMsg = dataStr
                    };

                    return new BadRequestObjectResult(result);
                };
            });
            services.AddTransient<IConfigureOptions<SwaggerGenOptions>, SwaggerConfigureOptions>();
            #endregion

            //注册雪花ID算法示例
            services.AddSingleton(new Common.SnowflakeHelper(0, 0));

            #region 缓存服务模式
            //注册缓存服务 内存模式
            services.AddDistributedMemoryCache();


            //注册缓存服务 SqlServer模式
            //services.AddDistributedSqlServerCache(options =>
            //{
            //    options.ConnectionString = Configuration.GetConnectionString("dbConnection");
            //    options.SchemaName = "dbo";
            //    options.TableName = "t_cache";
            //});


            //注册缓存服务 Redis模式
            //services.AddStackExchangeRedisCache(options =>
            //{
            //    options.Configuration = Configuration.GetConnectionString("redisConnection");
            //    options.InstanceName = "cache";
            //});
            #endregion

            #region 分布式锁服务注册
            services.AddSingleton<IDistributedLockProvider>(new SqlDistributedSynchronizationProvider(Configuration.GetConnectionString("dbConnection")));
            services.AddSingleton<IDistributedSemaphoreProvider>(new SqlDistributedSynchronizationProvider(Configuration.GetConnectionString("dbConnection")));
            services.AddSingleton<IDistributedUpgradeableReaderWriterLockProvider>(new SqlDistributedSynchronizationProvider(Configuration.GetConnectionString("dbConnection")));
            #endregion
            return services;
        }

        /// <summary>
        /// 使用UseKevin
        /// UseKevin
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseKevin(this IApplicationBuilder app)
        {
            ///json压缩
            app.UseResponseCompression();
            app.UseHsts(); 
            //注册跨域信息
            app.UseCors("cors");
            //强制重定向到Https
            app.UseHttpsRedirection();

            //静态文件中间件 (UseStaticFiles) 返回静态文件，并简化进一步请求处理。
            //app.UseStaticFiles();

            app.UseRouting();

            //注册用户认证机制,必须放在 UseCors UseRouting 之后
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            //启用中间件服务生成Swagger作为JSON端点
            app.UseSwagger();


            GlobalServices.ServiceProvider = app.ApplicationServices;
            return app;
        }
    }
}