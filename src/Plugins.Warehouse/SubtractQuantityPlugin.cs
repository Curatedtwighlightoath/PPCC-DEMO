using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using TALXIS.DevKit.Build.Dataverse.Plugin;

namespace Plugins.Warehouse
{
    // Post-operation: runs inside the database transaction, so the stock update and the
    // transaction record commit or roll back together.
    [PluginStep("Create", "lab_warehousetransaction", PluginStage.PostOperation)]
    public class SubtractQuantityPlugin : PluginBase
    {
        private const int Inbound = 100000000;
        private const int Outbound = 100000001;

        public SubtractQuantityPlugin(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(SubtractQuantityPlugin))
        {
        }

        protected override void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
            {
                throw new ArgumentNullException(nameof(localPluginContext));
            }

            var context = localPluginContext.PluginExecutionContext;
            var service = localPluginContext.OrgSvcFactory.CreateOrganizationService(context.UserId);

            if (!(context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity target) || target.LogicalName != "lab_warehousetransaction")
                return;

            if (!target.Contains("lab_quantity") || !target.Contains("lab_itemid") || !target.Contains("lab_transactiontype"))
                return;

            var quantity = (int)target["lab_quantity"];
            var itemRef = (EntityReference)target["lab_itemid"];
            var transactionType = (OptionSetValue)target["lab_transactiontype"];

            var item = service.Retrieve("lab_warehouseitem", itemRef.Id, new ColumnSet("lab_availablequantity"));
            var available = item.Contains("lab_availablequantity") ? (int)item["lab_availablequantity"] : 0;

            if (transactionType.Value == Inbound)
                item["lab_availablequantity"] = available + quantity;
            else if (transactionType.Value == Outbound)
                item["lab_availablequantity"] = available - quantity;
            else
                return;

            service.Update(item);
        }
    }
}
