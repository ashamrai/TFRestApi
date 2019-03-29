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
        static readonly string TFUrl = "https://dev.azure.com/<org>/"; // for azure devops
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

                string teamProjectName = "<Team Project Name>";
                int testPlanId = 1177;  // Existing test plan Id              
                string staticSuiteName = "Static Suite"; // Path to test suite with test cases

                CreateTestResultFailed(teamProjectName, testPlanId, staticSuiteName);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Create failed test results for all tests in test suite
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TestPlanId"></param>
        /// <param name="StaticSuitePath"></param>
        private static void CreateTestResultFailed(string TeamProjectName, int TestPlanId, string StaticSuitePath)
        {
            TestPlan testPlan = TestManagementClient.GetPlanByIdAsync(TeamProjectName, TestPlanId).Result;
            int testSuiteId = GetSuiteId(TeamProjectName, TestPlanId, StaticSuitePath);

            var testPlanRef = new Microsoft.TeamFoundation.TestManagement.WebApi.ShallowReference(testPlan.Id.ToString(), testPlan.Name, testPlan.Url);

            RunCreateModel runCreate = new RunCreateModel(
                name: "Test run from console - failed",
                plan: testPlanRef,
                startedDate: DateTime.Now.ToString("o"),
                isAutomated: true
                );

            TestRun testRun = TestManagementClient.CreateTestRunAsync(runCreate, TeamProjectName).Result;

            List<TestCaseResult> testResults = new List<TestCaseResult>();

            List<SuiteTestCase> testCases = TestManagementClient.GetTestCasesAsync(TeamProjectName, TestPlanId, testSuiteId).Result; //Get all test cases from suite

            foreach (var testCase in testCases) testResults.Add(FailedTest(TeamProjectName, TestPlanId, testSuiteId, testCase.Workitem.Id, testRun.Id));

            testResults = TestManagementClient.AddTestResultsToTestRunAsync(testResults.ToArray(), TeamProjectName, testRun.Id).Result;

            var definedTestResults = TestManagementClient.GetTestResultsAsync(TeamProjectName, testRun.Id).Result; // Get test result
            
            TestManagementClient.CreateTestResultAttachmentAsync(GetAttachmentModel(@"img\iconfinder_Insect-robot_131435.png"), TeamProjectName, testRun.Id, definedTestResults.ElementAt(0).Id).Wait();
            

            RunUpdateModel runUpdateModel = new RunUpdateModel(
                errorMessage: "Test failed",
                completedDate: DateTime.Now.ToString("o"),
                state: Enum.GetName(typeof(TestRunState), TestRunState.NeedsInvestigation)
                );

            testRun = TestManagementClient.UpdateTestRunAsync(runUpdateModel, TeamProjectName, testRun.Id).Result;

            TestManagementClient.CreateTestRunAttachmentAsync(GetAttachmentModel(@"img\Screen_Shot_2018-01-16.jpg"), TeamProjectName, testRun.Id).Wait();

            PrintBasicRunInfo(testRun);
        }

        /// <summary>
        /// Create attachment model
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        static TestAttachmentRequestModel GetAttachmentModel(string path)
        {
            string[] pathArr = path.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            Byte[] bytes = File.ReadAllBytes(path);
            return new TestAttachmentRequestModel(Convert.ToBase64String(bytes), pathArr[pathArr.Length - 1]);
        }

        /// <summary>
        /// Create struct for failed test
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TestPlanId"></param>
        /// <param name="TestSuiteId"></param>
        /// <param name="TestCaseId"></param>
        /// <param name="TestRunId"></param>
        /// <returns></returns>
        static TestCaseResult FailedTest(string TeamProjectName, int TestPlanId, int TestSuiteId, string TestCaseId, int TestRunId)
        {
            TestPoint testPoint = TestManagementClient.GetPointsAsync(TeamProjectName, TestPlanId, TestSuiteId, testCaseId: TestCaseId).Result.FirstOrDefault();

            TestCaseResult testCaseResult = new TestCaseResult();
            testCaseResult.Outcome = Enum.GetName(typeof(TestOutcome), TestOutcome.Failed);
            testCaseResult.TestPoint = new Microsoft.TeamFoundation.TestManagement.WebApi.ShallowReference(testPoint.Id.ToString(), url: testPoint.Url);
            testCaseResult.CompletedDate = DateTime.Now;
            testCaseResult.ErrorMessage = "Test Case " + TestCaseId + " failed";
            testCaseResult.State = Enum.GetName(typeof(TestRunState), TestRunState.Completed);
            testCaseResult.StackTrace = "Add StackTrace here";

            Dictionary<string, object> bugFields = new Dictionary<string, object>();
            bugFields.Add("Title", "Bug from console test - " + TestCaseId);

            //create new bug
            WorkItem bug = CreateWorkItem(TeamProjectName, "Bug", bugFields);

            var bugs = new List<Microsoft.TeamFoundation.TestManagement.WebApi.ShallowReference>();
            bugs.Add(new Microsoft.TeamFoundation.TestManagement.WebApi.ShallowReference(bug.Id.ToString(), url: bug.Url));

            testCaseResult.AssociatedBugs = bugs;           

            return testCaseResult;
        }

        /// <summary>
        /// Create work item
        /// </summary>
        /// <param name="ProjectName"></param>
        /// <param name="WorkItemTypeName"></param>
        /// <param name="Fields"></param>
        /// <returns></returns>
        static WorkItem CreateWorkItem(string ProjectName, string WorkItemTypeName, Dictionary<string, object> Fields)
        {
            JsonPatchDocument patchDocument = new JsonPatchDocument();

            foreach (var key in Fields.Keys)
                patchDocument.Add(new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = "/fields/" + key,
                    Value = Fields[key]
                });

            return WitClient.CreateWorkItemAsync(patchDocument, ProjectName, WorkItemTypeName).Result;
        }

        /// <summary>
        /// Get ID on an existing test suite by path in test plan
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TestPlanId"></param>
        /// <param name="SuitePath"></param>
        /// <returns></returns>
        static int GetSuiteId(string TeamProjectName, int TestPlanId, string SuitePath)
        {
            TestPlan testPlan = TestManagementClient.GetPlanByIdAsync(TeamProjectName, TestPlanId).Result;                       

            int parentSuiteId = 0;

            if (int.TryParse(testPlan.RootSuite.Id, out parentSuiteId))
            {
                if (SuitePath == "") return parentSuiteId;

                string[] pathArray = SuitePath.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < pathArray.Length; i++)
                {
                    TestSuite testSuite = TestManagementClient.GetTestSuiteByIdAsync(TeamProjectName, TestPlanId, parentSuiteId, 1).Result;
                    
                    string parentSuiteIdStr = (from ts in testSuite.Suites where ts.Name == pathArray[i] select ts.Id).FirstOrDefault();

                    if (!int.TryParse(parentSuiteIdStr, out parentSuiteId)) break;

                    if (i == pathArray.Length - 1) return parentSuiteId;
                }                
            }

            return 0;
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
