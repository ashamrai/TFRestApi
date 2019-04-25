using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.TestManagement.TestPlanning.WebApi;
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

         static void Main(string[] args)
        {
            try
            {
                ConnectWithPAT(TFUrl, UserPAT);

                string teamProjectName = "<Team Project Name>";
                string testPlanName = "New Test Plan Sample";
                string testPlanArea = "";
                string testPlanIteration = "";
                DateTime testPlanStartDate = DateTime.Now;
                DateTime testPlanFinishDate = DateTime.Now.AddDays(14);
                string staticSuiteName = "Static Suite";
                string dynamicSuiteName = "Dynamic Suite";
                string folderForRequirements = "Requirements";
                string dynamicQuery = "SELECT [System.Id] FROM WorkItems WHERE[System.WorkItemType] IN GROUP 'Microsoft.TestCaseCategory'"; // query to select all test cases
                int[] reqIds = { 0, 0 }; //update to existing requirements (user stories, product backlog items) ids

                int testPlanId = CreateTestPlan(teamProjectName, testPlanName, testPlanStartDate, testPlanFinishDate, testPlanArea, testPlanIteration);        

                CreateTestSuite(teamProjectName, testPlanId, staticSuiteName);
                CreateTestSuite(teamProjectName, testPlanId, dynamicSuiteName, TestSuiteType.DynamicTestSuite, staticSuiteName, SuiteQuery: dynamicQuery);
                CreateTestSuite(teamProjectName, testPlanId, folderForRequirements, ParentPath: staticSuiteName);

                foreach (int rId in reqIds)
                    CreateTestSuite(teamProjectName, testPlanId, SuiteType: TestSuiteType.RequirementTestSuite, ParentPath: staticSuiteName + "\\" + folderForRequirements, RequirementId: rId);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
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
        /// <param name="SuiteType"></param>
        /// <param name="ParentPath"></param>
        /// <param name="SuiteQuery"></param>
        /// <param name="RequirementIds"></param>
        /// <returns></returns>
        static bool CreateTestSuite(string TeamProjectName, int TestPlanId, string TestSuiteName = "", TestSuiteType SuiteType = TestSuiteType.StaticTestSuite, string ParentPath = "", string SuiteQuery = "", int RequirementId = 0)
        {
            switch(SuiteType)
            {
                case TestSuiteType.StaticTestSuite: if (TestSuiteName == "") { Console.WriteLine("Set the name for the test suite"); return false; }
                    break;
                case TestSuiteType.DynamicTestSuite: if (TestSuiteName == "") { Console.WriteLine("Set the name for the test suite"); return false; }
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
            

            int parentsuiteId = GetParentSuiteId(TeamProjectName, TestPlanId, ParentPath);

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
        static int GetParentSuiteId(string TeamProjectName, int TestPlanId, string SuitePath)
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
