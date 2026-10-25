using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using TALXIS.DevKit.Build.Dataverse.Plugin;

namespace Plugins.Warehouse
{
    // Pre-validation: runs before the database transaction starts, the cheapest place to reject bad input.
    [PluginStep("Create", "lab_warehousetransaction", PluginStage.PreValidation)]
    public class ValidateWarehouseTransactionPlugin : PluginBase
    {
        private const int Outbound = 100000001;

        public ValidateWarehouseTransactionPlugin(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(ValidateWarehouseTransactionPlugin))
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
            var tracingService = localPluginContext.TracingService;

            if (!(context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity target) || target.LogicalName != "lab_warehousetransaction")
                return;

            if (!target.Contains("lab_quantity") || !target.Contains("lab_itemid") || !target.Contains("lab_transactiontype"))
                return;

            // Inbound is never validated
            var transactionType = (OptionSetValue)target["lab_transactiontype"];
            if (transactionType.Value != Outbound)
                return;

            try
            {
                var quantity = (int)target["lab_quantity"];
                var itemRef = (EntityReference)target["lab_itemid"];

                var item = service.Retrieve("lab_warehouseitem", itemRef.Id, new ColumnSet("lab_availablequantity"));
                var available = item != null && item.Contains("lab_availablequantity") ? (int)item["lab_availablequantity"] : 0;

                if (quantity > available)
                {
                    throw new InvalidPluginExecutionException(
                        $"Not enough product in stock. Available: {available}, requested: {quantity}.");
                }
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracingService.Trace("Plugin Exception: {0}", ex.ToString());
                throw;
            }
        }
    }
}
