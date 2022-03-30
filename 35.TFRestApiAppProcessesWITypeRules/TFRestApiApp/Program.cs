using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.Process.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.Process.WebApi.Models;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TFRestApiApp
{
    class Program
    {
        static readonly string AzDOUrl = "https://dev.azure.com/<org>";
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<pat>"; //https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops

        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;
        static WorkItemTrackingProcessHttpClient ProcessHttpClient;

        static void Main(string[] args)
        {
            ConnectWithPAT(AzDOUrl, UserPAT);
            string processName = "My New Process"; //existing process
            string witName = "Task"; //existing work item type
            Guid procId;
            string witRefName;

            GetProcAndWIT(processName, witName, out procId, out witRefName);
            UpdateTitleonNew(procId, witRefName);
            MakeActiveTitleReadOnly(procId, witRefName);

            ShowAllRules(procId, witRefName);
            DisableRule("Update Title", procId, witRefName);
            DeleteRule("Title Read Only", procId, witRefName);
        }

        /// <summary>
        /// Disable existing rule
        /// </summary>
        /// <param name="title"></param>
        /// <param name="procId"></param>
        /// <param name="witRefName"></param>
        private static void DisableRule(string title, Guid procId, string witRefName)
        {
            var rules = ProcessHttpClient.GetProcessWorkItemTypeRulesAsync(procId, witRefName).Result;

            var rule = (from r in rules where r.Name == title select r).FirstOrDefault();

            if (rule != null)
            {
                UpdateProcessRuleRequest updateProcessRule = new UpdateProcessRuleRequest();
                updateProcessRule.Name = rule.Name;
                updateProcessRule.Actions = rule.Actions;
                updateProcessRule.Conditions = rule.Conditions;
                updateProcessRule.IsDisabled = true;

                var result = ProcessHttpClient.UpdateProcessWorkItemTypeRuleAsync(updateProcessRule, procId, witRefName, rule.Id).Result;
            }
        }

        /// <summary>
        /// Remove existing rule
        /// </summary>
        /// <param name="title"></param>
        /// <param name="procId"></param>
        /// <param name="witRefName"></param>
        private static void DeleteRule(string title, Guid procId, string witRefName)
        {
            var rules = ProcessHttpClient.GetProcessWorkItemTypeRulesAsync(procId, witRefName).Result;

            var rule = (from r in rules where r.Name == title select r).FirstOrDefault();

            if (rule != null)
            {
                ProcessHttpClient.DeleteProcessWorkItemTypeRuleAsync(procId, witRefName, rule.Id).Wait();
            }
        }

        /// <summary>
        /// View all rules
        /// </summary>
        /// <param name="procId"></param>
        /// <param name="witRefName"></param>
        private static void ShowAllRules(Guid procId, string witRefName)
        {
            var rules = ProcessHttpClient.GetProcessWorkItemTypeRulesAsync(procId, witRefName).Result;

            foreach (var rule in rules)
            {
                Console.WriteLine("---------------------------------------------------------");
                Console.WriteLine("{0} : {1}", rule.CustomizationType, rule.Name);
                Console.WriteLine("------------------Conditions-----------------------------");

                foreach (var condition in rule.Conditions)
                    Console.WriteLine("{0}  {1}  {2}", condition.ConditionType, condition.Field, condition.Value);

                Console.WriteLine("------------------Actions-------------------------------");

                foreach (var action in rule.Actions)
                    Console.WriteLine("{0}  {1}  {2}", action.ActionType, action.TargetField, action.Value);

                Console.WriteLine("========================================================");
            }
        }

        /// <summary>
        /// Add new rule
        /// </summary>
        /// <param name="procId"></param>
        /// <param name="witRefName"></param>
        private static void UpdateTitleonNew(Guid procId, string witRefName)
        {
            RuleAction ruleAction = new RuleAction();
            ruleAction.ActionType = RuleActionType.CopyFromClock;
            ruleAction.TargetField = "System.Title";
            RuleCondition ruleCondition = new RuleCondition();
            ruleCondition.ConditionType = RuleConditionType.WhenChanged;
            ruleCondition.Field = "System.State";
            ruleCondition.Value = "New";

            CreateProcessRuleRequest createProcessRule = new CreateProcessRuleRequest();
            createProcessRule.Name = "Update Title";
            createProcessRule.Conditions = new List<RuleCondition> { ruleCondition };
            createProcessRule.Actions = new List<RuleAction> { ruleAction };

            var rule = ProcessHttpClient.AddProcessWorkItemTypeRuleAsync(createProcessRule, procId, witRefName).Result;
        }

        /// <summary>
        /// Add new rule
        /// </summary>
        /// <param name="procId"></param>
        /// <param name="witRefName"></param>
        private static void MakeActiveTitleReadOnly(Guid procId, string witRefName)
        {
            RuleAction ruleAction = new RuleAction();
            ruleAction.ActionType = RuleActionType.MakeReadOnly;
            ruleAction.TargetField = "System.Title";
            RuleCondition ruleCondition = new RuleCondition();
            ruleCondition.ConditionType = RuleConditionType.WhenNot;
            ruleCondition.Field = "System.State";
            ruleCondition.Value = "New";

            CreateProcessRuleRequest createProcessRule = new CreateProcessRuleRequest();
            createProcessRule.Name = "Title Read Only";
            createProcessRule.Conditions = new List<RuleCondition> { ruleCondition };
            createProcessRule.Actions = new List<RuleAction> { ruleAction };

            var rule = ProcessHttpClient.AddProcessWorkItemTypeRuleAsync(createProcessRule, procId, witRefName).Result;
        }

        /// <summary>
        /// Create a inherited work item type to make changes in the work flow
        /// </summary>
        /// <param name="procId"></param>
        /// <param name="parentRef"></param>
        /// <returns></returns>

        static private string CreateInheritedWIT(Guid procId, string parentRef)
        {
            var witDef = ProcessHttpClient.GetProcessWorkItemTypeAsync(procId, parentRef).Result;
            CreateProcessWorkItemTypeRequest cpwit = new CreateProcessWorkItemTypeRequest();
            cpwit.Color = witDef.Color;
            cpwit.Icon = witDef.Icon;
            cpwit.InheritsFrom = parentRef;

            var newwit = ProcessHttpClient.CreateProcessWorkItemTypeAsync(cpwit, procId).Result;

            return newwit.ReferenceName;
        }

        

        /// <summary>
        /// Get process id and work item type reference name by they names
        /// </summary>
        /// <param name="processName"></param>
        /// <param name="witName"></param>
        /// <param name="procId"></param>
        /// <param name="witRefName"></param>
        private static void GetProcAndWIT(string processName, string witName, out Guid procId, out string witRefName)
        {
            procId = GetProcessGuid(processName);
            if (procId == null)
            {
                throw new Exception("Can not find process.");
            }

            witRefName = GetWITrefName(procId, witName);
            if (string.IsNullOrEmpty(witRefName))
            {
                throw new Exception("Can not find work item type.");
            }
        }

        private static Guid GetProcessGuid(string processName)
        {
            Guid newProcessGuid = Guid.Empty;

            var processes = ProcessHttpClient.GetListOfProcessesAsync().Result;

            return (from p in processes where p.Name == processName select p.TypeId).FirstOrDefault();
        }

        private static string GetWITrefName(Guid procGuid, string witName)
        {
            var wiTypes = ProcessHttpClient.GetProcessWorkItemTypesAsync(procGuid).Result;

            return (from p in wiTypes where p.Name == witName select p.ReferenceName).FirstOrDefault();
        }

        #region create new connections
        static void InitClients(VssConnection Connection)
        {
            WitClient = Connection.GetClient<WorkItemTrackingHttpClient>();
            BuildClient = Connection.GetClient<BuildHttpClient>();
            ProjectClient = Connection.GetClient<ProjectHttpClient>();
            GitClient = Connection.GetClient<GitHttpClient>();
            TfvsClient = Connection.GetClient<TfvcHttpClient>();
            TestManagementClient = Connection.GetClient<TestManagementHttpClient>();
            ProcessHttpClient = Connection.GetClient<WorkItemTrackingProcessHttpClient>();
        }

        static void ConnectWithDefaultCreds(string ServiceURL)
        {
            VssConnection connection = new VssConnection(new Uri(ServiceURL), new VssCredentials());
            InitClients(connection);
        }

        static void ConnectWithCustomCreds(string ServiceURL, string User, string Password)
        {
            VssConnection connection = new VssConnection(new Uri(ServiceURL), new WindowsCredential(new NetworkCredential(User, Password)));
            InitClients(connection);
        }

        static void ConnectWithPAT(string ServiceURL, string PAT)
        {
            VssConnection connection = new VssConnection(new Uri(ServiceURL), new VssBasicCredential(string.Empty, PAT));
            InitClients(connection);
        }
        #endregion
    }
}
