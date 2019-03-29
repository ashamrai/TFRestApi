using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TFRestApiApp
{     


    class Program
    {
        //static readonly string TFUrl = "http://tfs-srv:8080/tfs/DefaultCollection/"; //for tfs
        static readonly string TFUrl = "https://dev.azure.com/<org_name>/"; // for devops azure 
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<pat>"; //https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops

        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestManagementHttpClient TestManagementClient;

        static void Main(string[] args)
        {
            try
            {
                ConnectWithPAT(TFUrl, UserPAT);

                string teamProjectName = "<Project Name>";

                CreateTestResultCompleted(teamProjectName);
                CreateTestResultFailed(teamProjectName);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Create success test result
        /// </summary>
        /// <param name="TeamProjectName"></param>
        private static void CreateTestResultCompleted(string TeamProjectName)
        {
            RunCreateModel runCreate = new RunCreateModel(
                name: "Test run from console - completed",
                startedDate: DateTime.Now.ToString("o"),                
                isAutomated: true                
                );

            TestRun testRun = TestManagementClient.CreateTestRunAsync(runCreate, TeamProjectName).Result;

            TestCaseResult testCaseResult = new TestCaseResult();
            testCaseResult.AutomatedTestName = "MyTestSuite.TestName";
            testCaseResult.TestCaseTitle = "Check my function";
            testCaseResult.Outcome = Enum.GetName(typeof(TestOutcome), TestOutcome.Passed);
            testCaseResult.CompletedDate = DateTime.Now;
            testCaseResult.State = Enum.GetName(typeof(TestRunState), TestRunState.Completed);

            TestManagementClient.AddTestResultsToTestRunAsync(new TestCaseResult[] { testCaseResult }, TeamProjectName, testRun.Id).Wait();

            RunUpdateModel runUpdateModel = new RunUpdateModel(
                completedDate: DateTime.Now.ToString("o"),
                state: Enum.GetName(typeof(TestRunState), TestRunState.Completed)                
                );

            testRun = TestManagementClient.UpdateTestRunAsync(runUpdateModel, TeamProjectName, testRun.Id).Result;

            PrintBasicRunInfo(testRun);
                    
        }

        /// <summary>
        /// Create failed test results
        /// </summary>
        /// <param name="TeamProjectName"></param>
        private static void CreateTestResultFailed(string TeamProjectName)
        {
            RunCreateModel runCreate = new RunCreateModel(
                name: "Test run from console - failed",
                startedDate: DateTime.Now.ToString("o"),
                isAutomated: true
                );

            TestRun testRun = TestManagementClient.CreateTestRunAsync(runCreate, TeamProjectName).Result;

            TestCaseResult testCaseResult = new TestCaseResult();
            testCaseResult.AutomatedTestName = "MyTestSuite.TestName";
            testCaseResult.TestCaseTitle = "Check my function";
            testCaseResult.StackTrace = "Add StackTrace here";
            testCaseResult.ErrorMessage = "Test 'MyTestSuite.TestName' failed";
            testCaseResult.Outcome = Enum.GetName(typeof(TestOutcome), TestOutcome.Failed);
            testCaseResult.CompletedDate = DateTime.Now;
            testCaseResult.State = Enum.GetName(typeof(TestRunState), TestRunState.Completed);

            TestManagementClient.AddTestResultsToTestRunAsync(new TestCaseResult[] { testCaseResult }, TeamProjectName, testRun.Id).Wait();

            RunUpdateModel runUpdateModel = new RunUpdateModel(
                completedDate: DateTime.Now.ToString("o"),
                state: Enum.GetName(typeof(TestRunState), TestRunState.NeedsInvestigation)
                );

            testRun = TestManagementClient.UpdateTestRunAsync(runUpdateModel, TeamProjectName, testRun.Id).Result;

            PrintBasicRunInfo(testRun);
        }

        static void PrintBasicRunInfo(TestRun testRun)
        {
            Console.WriteLine("Information for test run:" + testRun.Id);
            Console.WriteLine("Automated - {0}; Start Date - '{1}'; Completed date - '{2}'", (testRun.IsAutomated) ? "Yes" : "No", testRun.StartedDate.ToString(), testRun.CompletedDate.ToString());
            Console.WriteLine("Total tests - {0}; Passed tests - {1}", testRun.TotalTests, testRun.PassedTests);
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
