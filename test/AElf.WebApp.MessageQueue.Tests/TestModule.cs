using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Kernel;
using AElf.Kernel.Account.Application;
using AElf.Kernel.Blockchain.Application;
using AElf.OS;
using AElf.OS.Node.Application;
using AElf.Types;
using AElf.WebApp.MessageQueue;
using AElf.WebApp.MessageQueue.Helpers;
using AElf.WebApp.MessageQueue.Provider;
using AElf.WebApp.MessageQueue.Services;
using AElf.WebApp.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Volo.Abp;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Autofac;
using Volo.Abp.EventBus;
using Volo.Abp.Modularity;
using Volo.Abp.Threading;

namespace AElf.WebApp.Application.MessageQueue.Tests;


[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreTestBaseModule),
    typeof(MessageQueueAElfModule),
    typeof(KernelCoreTestAElfModule),
    typeof(AbpEventBusModule)
)]
public class TestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        base.ConfigureServices(context);
        var services = context.Services;
        //需要mock chain
        //services.AddSingleton(p => Mock.Of<>());
        services.AddDistributedMemoryCache();
        services.AddSingleton<ISyncBlockStateProvider, SycTestProvider>();
        services.AddSingleton<SendMessageServer>();
        //services.AddSingleton<IBlockMessageService,BlockMessageService>();
    }
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var kernelTestHelper = context.ServiceProvider.GetService<KernelTestHelper>();
        var previousBlockHeader = kernelTestHelper.BestBranchBlockList.Last().Header;
        var chain1 = AsyncHelper.RunSync(() => kernelTestHelper.MockChainAsync());
        //var chain = AsyncHelper.RunSync(() => MockChainAsync(kernelTestHelper));
        var transactions =
            kernelTestHelper.GenerateTransactions(3, previousBlockHeader.Height, previousBlockHeader.PreviousBlockHash);
        var transactionResult =
            kernelTestHelper.GenerateTransactionResult(transactions[0], TransactionResultStatus.Mined);

    }


}