using System;
using Micros.Ops.Extensibility;
using YourNamespace.Contracts.Clients;
using YourNamespace.Helpers;

namespace YourNamespace.Clients.OpsContext
{
    /// <summary>
    /// Implementation of IOpsContextClient for interacting with Simphony OpsContext.
    /// CRITICAL: All OpsContext calls MUST use the Invoke() wrapper for thread safety.
    /// Place in: Clients/OpsContext/SimphonyOpsContextClient.cs
    /// </summary>
    public class SimphonyOpsContextClient : IOpsContextClient
    {
        private readonly Micros.Ops.OpsContext _opsContext;
        private readonly string _title;

        public SimphonyOpsContextClient(OpsExtensibilityEnvironment environment)
        {
            _opsContext = environment.OpsContext;
            _title = VersionHelper.NameAndVersion;
        }

        /// <summary>
        /// CRITICAL: Executes action on Simphony command thread for thread safety.
        /// ALL OpsContext calls MUST be wrapped with this method.
        /// </summary>
        private void Invoke(Action a)
        {
            _opsContext.InvokeOnCommandThread(delegate
            {
                a.Invoke();
                return null;
            }, null);
        }

        public void ShowMessage(string message)
        {
            Invoke(() => _opsContext.ShowMessage(message));
        }

        public void ShowError(string message)
        {
            Invoke(() => _opsContext.ShowError(message));
        }

        public bool AskQuestion(string message)
        {
            var result = false;
            Invoke(() => result = _opsContext.AskQuestion(message));
            return result;
        }

        public decimal? GetAmountInput(string message)
        {
            decimal? result = null;
            Invoke(() => result = _opsContext.RequestAmountEntry(message, _title));
            return result;
        }

        public string GetTextInput(string message)
        {
            var result = "";
            Invoke(() => result = _opsContext.RequestAlphaEntry(message, _title));
            return result;
        }

        public int? SelectFromList(string title, string prompt, string[] items)
        {
            int? result = null;
            Invoke(() => result = _opsContext.SelectionRequest(title, prompt, items));
            return result;
        }

        // EXPAND AS NEEDED:
        // Add more methods to IOpsContextClient interface and implement them here
        // Always follow these patterns:
        //
        // 1. For void operations (no return value):
        //    public void SomeOperation(string param)
        //    {
        //        Invoke(() => _opsContext.SomeMethod(param));
        //    }
        //
        // 2. For operations with return values:
        //    public string SomeOperation(string param)
        //    {
        //        var result = "";
        //        Invoke(() => result = _opsContext.SomeMethod(param));
        //        return result;
        //    }
        //
        // 3. For complex operations with OpsCommand:
        //    using Micros.Ops;
        //
        //    public void PostServiceCharge(int number, decimal amount)
        //    {
        //        var command = new OpsCommand
        //        {
        //            Command = OpsCommandType.ServiceCharge,
        //            Number = number
        //        };
        //        Invoke(() => _opsContext.ProcessCommand(command));
        //    }
        //
        // See input examples for full implementation patterns
    }
}
