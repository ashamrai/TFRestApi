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
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TFRestApiApp
{
    class Program
    {
        //static readonly string TFUrl = "http://tfs-srv:8080/tfs/DefaultCollection/"; //for tfs
        static readonly string TFUrl = "https://dev.azure.com/<your_org>/"; // for devops azure 
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<your_pat>"; //https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops

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

                string teamProjectName = "Team Project name";
                string testPlanName = "New Test Plan";
                string testPlanArea = "";
                string testPlanIteration = "";
                DateTime testPlanStartDate = DateTime.Now;
                DateTime testPlanFinishDate = DateTime.Now.AddDays(14);
                string staticSuiteName = "Static Suite";
                string dynamicSuiteName = "Dynamic Suite";
                string folderForRequirements = "Requirements";
                string dynamicQuery = "SELECT [System.Id] FROM WorkItems WHERE[System.WorkItemType] IN GROUP 'Microsoft.TestCaseCategory'"; // query to select all test cases
                int[] reqIds = { -1, -1, -1 }; //update to existing requirements (user stories, product backlog items) ids

                int testPlanId = CreateTestPlan(teamProjectName, testPlanName, testPlanStartDate, testPlanFinishDate, testPlanArea, testPlanIteration);        

                CreateTestSuite(teamProjectName, testPlanId, staticSuiteName);
                CreateTestSuite(teamProjectName, testPlanId, dynamicSuiteName, "DynamicTestSuite", staticSuiteName, dynamicQuery);
                CreateTestSuite(teamProjectName, testPlanId, folderForRequirements, ParentPath: staticSuiteName);
                CreateTestSuite(teamProjectName, testPlanId, TestSuiteType: "RequirementTestSuite", ParentPath: staticSuiteName + "\\" + folderForRequirements, RequirementIds: reqIds);
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
            Microsoft.TeamFoundation.TestManagement.WebApi.ShallowReference areaRef = null;

            if (AreaPath != "")
            {
                var area = WitClient.GetClassificationNodeAsync(TeamProjectName, TreeStructureGroup.Areas, AreaPath).Result;
                areaRef = new Microsoft.TeamFoundation.TestManagement.WebApi.ShallowReference() {
                    Id = area.Identifier.ToString(),
                    Name = TeamProjectName + "\\" + AreaPath,
                    Url = area.Url
                };
            }

            if (IterationPath != "") IterationPath = TeamProjectName + "\\" + IterationPath;

            PlanUpdateModel newPlanDef = new PlanUpdateModel(
                name: TestPlanName,
                startDate: (StartDate.HasValue) ? StartDate.Value.ToString("o") : "",
                endDate: (FinishDate.HasValue) ? FinishDate.Value.ToString("o") : "",
                area: areaRef,
                iteration: IterationPath
                );           


            return TestManagementClient.CreateTestPlanAsync(newPlanDef, TeamProjectName).Result.Id;
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
        static bool CreateTestSuite(string TeamProjectName, int TestPlanId, string TestSuiteName = "", string TestSuiteType = "StaticTestSuite", string ParentPath = "", string SuiteQuery = "", int[] RequirementIds = null)
        {
            switch(TestSuiteType)
            {
                case "StaticTestSuite": if (TestSuiteName == "") { Console.WriteLine("Set the name for the a test suite"); return false; }
                    break;
                case "DynamicTestSuite": if (TestSuiteName == "") { Console.WriteLine("Set the name for the a test suite"); return false; }
                    if (SuiteQuery == "") { Console.WriteLine("Set the query for the new a suite"); return false; }
                    break;
                case "RequirementTestSuite":
                    if (RequirementIds == null) { Console.WriteLine("Set the requrements set for the a test suite"); return false; }
                    break;
            }

            SuiteCreateModel newSuite = new SuiteCreateModel(TestSuiteType, TestSuiteName, SuiteQuery, RequirementIds);

            int parentsuiteId = GetParentSuiteId(TeamProjectName, TestPlanId, ParentPath);

            if (parentsuiteId > 0)
            {
                List<TestSuite> testSuiteList = TestManagementClient.CreateTestSuiteAsync(newSuite, TeamProjectName, TestPlanId, parentsuiteId).Result;
                foreach (TestSuite ts in testSuiteList)
                    Console.WriteLine("The Test Suite has been created: " + ts.Id + " - " + ts.Name);

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
