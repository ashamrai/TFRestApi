using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.TestManagement.TestPlanning.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
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
        //static readonly string TFUrl = "http://tfs-srv:8080/tfs/DefaultCollection/"; //for tfs
        static readonly string TFUrl = "https://dev.azure.com/<org>/"; // for devops azure 
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<pat>"; //https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops

        static WorkItemTrackingHttpClient WitClient;
        static BuildHttpClient BuildClient;
        static ProjectHttpClient ProjectClient;
        static GitHttpClient GitClient;
        static TfvcHttpClient TfvsClient;
        static TestPlanHttpClient TestPlanClient;

        const string FieldSteps = "Microsoft.VSTS.TCM.Steps";
        const string FieldParameters = "Microsoft.VSTS.TCM.Parameters";
        const string FieldDataSource = "Microsoft.VSTS.TCM.LocalDataSource";

        static void Main(string[] args)
        {
            

            try
            {
                ConnectWithPAT(TFUrl, UserPAT);

                string teamProjectName = "Team Project Name";
                string testPlanName = "Test Plan Name";
                string testPlanArea = @"";
                string testPlanIteration = @"";
                DateTime testPlanStartDate = DateTime.Now;
                DateTime testPlanFinishDate = DateTime.Now.AddDays(14);
                string staticSuiteName = "Static Suite Name";

                List<int> testcasesIds = new List<int>();

                testcasesIds.Add(CreateTest(teamProjectName));
                Console.WriteLine("Test Cases has been created: " + testcasesIds[0]);
                testcasesIds.Add(CreateTestWithParams(teamProjectName));
                Console.WriteLine("Test Cases has been created: " + testcasesIds[1]);

                int testPlanId = CreateTestPlan(teamProjectName, testPlanName, testPlanStartDate, testPlanFinishDate, testPlanArea, testPlanIteration);
                Console.WriteLine("The Test Plan has been created " + testPlanId);

                CreateTestSuite(teamProjectName, testPlanId, staticSuiteName);
                AddTestCasesToSuite(teamProjectName, testPlanId, staticSuiteName, testcasesIds);
                Console.WriteLine("Test Cases has been added to the Test Suite: " + staticSuiteName);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Add test cases to an exisitng static test suite
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TestPlanId"></param>
        /// <param name="StaticSuitePath"></param>
        /// <param name="TestCasesIds"></param>
        private static void AddTestCasesToSuite(string TeamProjectName, int TestPlanId, string StaticSuitePath, List<int> TestCasesIds)
        {
            int testSuiteId = GetSuiteId(TeamProjectName, TestPlanId, StaticSuitePath);

            if (testSuiteId == 0) { Console.WriteLine("Can not find the suite:" + StaticSuitePath); return; }

            TestSuite testSuite = TestPlanClient.GetTestSuiteByIdAsync(TeamProjectName, TestPlanId, testSuiteId).Result;

            if (testSuite.SuiteType == TestSuiteType.StaticTestSuite || testSuite.SuiteType == TestSuiteType.DynamicTestSuite)
            {
                List<SuiteTestCaseCreateUpdateParameters> suiteTestCaseCreateUpdate = new List<SuiteTestCaseCreateUpdateParameters>();

                foreach (int testCaseId in TestCasesIds)
                    suiteTestCaseCreateUpdate.Add(new SuiteTestCaseCreateUpdateParameters()
                    {
                        workItem = new Microsoft.VisualStudio.Services.TestManagement.TestPlanning.WebApi.WorkItem()
                        {
                            Id = testCaseId
                        }
                    });
                
                TestPlanClient.AddTestCasesToSuiteAsync(suiteTestCaseCreateUpdate, TeamProjectName, TestPlanId, testSuiteId).Wait();
            }
            else
                Console.WriteLine("The Test Suite '" + StaticSuitePath + "' is not static or requirement");
        }

        /// <summary>
        /// Create a simple test case
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <returns></returns>
        static int CreateTest(string TeamProjectName)
        {
            Dictionary<string, object> fields = new Dictionary<string, object>();

            LocalStepsDefinition stepsDefinition = new LocalStepsDefinition();
            stepsDefinition.AddStep("Run Application");
            stepsDefinition.AddStep("Check available functions", "Functions for user access levels");

            LocalTestParams testParams = new LocalTestParams();

            fields.Add("Title", "new test case");
            fields.Add(FieldSteps, stepsDefinition.StepsDefinitionStr);

            return CreateWorkItem(TeamProjectName, "Test Case", fields).Id.Value;
        }

        /// <summary>
        /// Create a test case with parameters
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <returns></returns>
        static int CreateTestWithParams(string TeamProjectName)
        {
            Dictionary<string, object> fields = new Dictionary<string, object>();         


            LocalStepsDefinition stepsDefinition = new LocalStepsDefinition();
            stepsDefinition.AddStep("Run Application");
            stepsDefinition.AddStep("Enter creds @user_name @user_password");
            stepsDefinition.AddStep("Check available functions", "Functions for: @user_role");

            LocalTestParams testParams = new LocalTestParams();

            testParams.AddParam("user_name", new string[] { "admin", "user", "manager" });
            testParams.AddParam("user_password", new string[] { "admin_pswrd", "user_pswrd", "manager_pswrd" });
            testParams.AddParam("user_role", new string[] { "Administrator", "Local User", "Shop Manager" });

            fields.Add("Title", "new test case");
            fields.Add(FieldSteps, stepsDefinition.StepsDefinitionStr);
            fields.Add(FieldParameters, testParams.ParamDefinitionStr);
            fields.Add(FieldDataSource, testParams.ParamDataSetStr);

            return CreateWorkItem(TeamProjectName, "Test Case", fields).Id.Value;
        }

        /// <summary>
        /// Create a work item
        /// </summary>
        /// <param name="ProjectName"></param>
        /// <param name="WorkItemTypeName"></param>
        /// <param name="Fields"></param>
        /// <returns></returns>
        static Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem CreateWorkItem(string ProjectName, string WorkItemTypeName, Dictionary<string, object> Fields)
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
        /// Create a new test plan
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TestPlanName"></param>
        /// <param name="StartDate"></param>
        /// <param name="FinishDate"></param>
        /// <param name="AreaPath"></param>
        /// <param name="IterationPath"></param>
        /// <returns></returns>
        static int CreateTestPlan(string TeamProjectName, string TestPlanName, DateTime? StartDate = null, DateTime? FinishDate = null, string AreaPath = "", string IterationPath = "")
        {
            if (IterationPath != "") IterationPath = TeamProjectName + "\\" + IterationPath;
            if (AreaPath != "") AreaPath = TeamProjectName + "\\" + AreaPath;

            TestPlanCreateParams newPlanDef = new TestPlanCreateParams()
            {
                Name = TestPlanName,
                StartDate = StartDate,
                EndDate = FinishDate,
                AreaPath = AreaPath,
                Iteration = IterationPath
            };

            return TestPlanClient.CreateTestPlanAsync(newPlanDef, TeamProjectName).Result.Id;
        }

        /// <summary>
        /// Create a new test suite
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TestPlanId"></param>
        /// <param name="TestSuiteName"></param>
        /// <param name="TestSuiteType"></param>
        /// <param name="ParentPath"></param>
        /// <param name="SuiteQuery"></param>
        /// <param name="RequirementIds"></param>
        /// <returns></returns>
        static bool CreateTestSuite(string TeamProjectName, int TestPlanId, string TestSuiteName = "", TestSuiteType SuiteType = TestSuiteType.StaticTestSuite, string ParentPath = "", string SuiteQuery = "", int RequirementId = 0)
        {
            switch (SuiteType)
            {
                case TestSuiteType.StaticTestSuite:
                    if (TestSuiteName == "") { Console.WriteLine("Set the name for the test suite"); return false; }
                    break;
                case TestSuiteType.DynamicTestSuite:
                    if (TestSuiteName == "") { Console.WriteLine("Set the name for the test suite"); return false; }
                    if (SuiteQuery == "") { Console.WriteLine("Set the query for the new a suite"); return false; }
                    break;
                case TestSuiteType.RequirementTestSuite:
                    if (RequirementId == 0) { Console.WriteLine("Set the requrement id for the test suite"); return false; }
                    break;
            }

            TestSuiteCreateParams newSuite = new TestSuiteCreateParams()
            {
                Name = TestSuiteName,
                SuiteType = SuiteType,
                QueryString = SuiteQuery,
                RequirementId = RequirementId
            };


            int parentsuiteId = GetSuiteId(TeamProjectName, TestPlanId, ParentPath);

            if (parentsuiteId > 0)
            {
                newSuite.ParentSuite = new TestSuiteReference() { Id = parentsuiteId };
                TestSuite testSuite = TestPlanClient.CreateTestSuiteAsync(newSuite, TeamProjectName, TestPlanId).Result;
                Console.WriteLine("The Test Suite has been created: " + testSuite.Id + " - " + testSuite.Name);

            }
            else { Console.WriteLine("Can not find the parent test suite"); return false; }

            return true;
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
            TestPlan testPlan = TestPlanClient.GetTestPlanByIdAsync(TeamProjectName, TestPlanId).Result;
            if (SuitePath == "") return testPlan.RootSuite.Id;

            List<TestSuite> testPlanSuites = TestPlanClient.GetTestSuitesForPlanAsync(TeamProjectName, TestPlanId, SuiteExpand.Children, asTreeView: true).Result;

            string[] pathArray = SuitePath.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

            TestSuite suiteMarker = testPlanSuites[0]; //first level is the root suite

            for (int i = 0; i < pathArray.Length; i++)
            {
                suiteMarker = (from ts in suiteMarker.Children where ts.Name == pathArray[i] select ts).FirstOrDefault();

                if (suiteMarker == null) return 0;

                if (i == pathArray.Length - 1) return suiteMarker.Id;
            }

            return 0;
        }       

        #region create new connections
        static void InitClients(VssConnection Connection)
        {
            WitClient = Connection.GetClient<WorkItemTrackingHttpClient>();
            BuildClient = Connection.GetClient<BuildHttpClient>();
            ProjectClient = Connection.GetClient<ProjectHttpClient>();
            GitClient = Connection.GetClient<GitHttpClient>();
            TfvsClient = Connection.GetClient<TfvcHttpClient>();
            TestPlanClient = Connection.GetClient<TestPlanHttpClient>();
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
