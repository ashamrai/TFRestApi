using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.VisualStudio.Services.TestManagement.TestPlanning.WebApi;
using Newtonsoft.Json;

namespace TFRestApiApp
{
    class Program
    {
        //static readonly string TFUrl = "http://tfs-srv:8080/tfs/DefaultCollection/"; //for tfs
        static readonly string TFUrl = "https://dev.azure.com/<org>/"; // for devops azure 
        static readonly string UserAccount = "";
        static readonly string UserPassword = "";
        static readonly string UserPAT = "<pat>";

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
                int testPlanId = 0; // set the plan id

                TestPlanDetails(teamProjectName, testPlanId);               
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                if (ex.InnerException != null) Console.WriteLine("Detailed Info: " + ex.InnerException.Message);
                Console.WriteLine("Stack:\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Get Test Plan Properties
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TestPlanId"></param>
        static void TestPlanDetails(string TeamProjectName, int TestPlanId)
        {
            TestPlan testPlan = TestPlanClient.GetTestPlanByIdAsync(TeamProjectName, TestPlanId).Result;

            Console.WriteLine("================================================================");
            Console.WriteLine("Test Plan  : {0} : {1} : {2}", testPlan.Id, testPlan.State, testPlan.Name);
            Console.WriteLine("Area Path  : {0} : Iteration Path : {1}", testPlan.AreaPath, testPlan.Iteration);
            Console.WriteLine("Plan Dates : {0} - {1}", 
                (testPlan.StartDate.HasValue) ? testPlan.StartDate.Value.ToShortDateString() : "none", 
                (testPlan.EndDate.HasValue) ? testPlan.EndDate.Value.ToShortDateString() : "none");

            //Get test suites by one request
            List<TestSuite> suitesDetail = TestPlanClient.GetTestSuitesForPlanAsync(TeamProjectName, TestPlanId, asTreeView: true).Result;

            ExploreTestSuiteTree(TeamProjectName, TestPlanId, suitesDetail, "");

            //Query each test suite
            //TestSuiteDetails(TeamProjectName, testPlan.Id, testPlan.RootSuite.Id, "");

        }

        /// <summary>
        /// View details of a test suite from a test suites list
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TestPlanId"></param>
        /// <param name="SuitesSubTree"></param>
        /// <param name="ParentPath"></param>
        static void ExploreTestSuiteTree(string TeamProjectName, int TestPlanId, List<TestSuite> SuitesSubTree, string ParentPath)
        {
            foreach (TestSuite testSuite in SuitesSubTree)
            {
                PrintSuiteInfo(testSuite, ParentPath);

                if (testSuite.HasChildren) ExploreTestSuiteTree(TeamProjectName, TestPlanId, testSuite.Children, ParentPath + "\\" + testSuite.Name);

                ViewTestCases(TeamProjectName, TestPlanId, testSuite);
            }
        }

        /// <summary>
        /// View a test cases list
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="TestPlanId"></param>
        /// <param name="testSuite"></param>
        private static void ViewTestCases(string TeamProjectName, int TestPlanId, TestSuite testSuite)
        {
            List<TestCase> testCases = TestPlanClient.GetTestCaseListAsync(TeamProjectName, TestPlanId, testSuite.Id).Result;

            if (testCases.Count > 0)
            {                
                foreach (TestCase testCase in testCases)
                {
                    Console.WriteLine("Test: {0} - {1}", testCase.workItem.Id, testCase.workItem.Name);

                    var wiFields = GetWorkItemFields(testCase.workItem.WorkItemFields);

                    if (wiFields.ContainsKey("System.State"))
                        Console.WriteLine("Test Case State: {0}", wiFields["System.State"].ToString());

                    foreach (var config in testCase.PointAssignments)
                        Console.WriteLine("Run for: {0} : {1}", config.Tester.DisplayName, config.ConfigurationName);
                }
            }
        }

        /// <summary>
        /// Convert an object list of work item fields to a dictionary
        /// </summary>
        /// <param name="WorkItemFieldsList"></param>
        /// <returns></returns>
        private static Dictionary<string, object> GetWorkItemFields(List<object> WorkItemFieldsList)
        {
            Dictionary<string, object> wiFields = new Dictionary<string, object>();

            foreach (object wiField in WorkItemFieldsList)
            {
                Dictionary<string, object> fld = JsonConvert.DeserializeObject<Dictionary<string, object>>(wiField.ToString());
                wiFields.Add(fld.Keys.ElementAt(0), fld[fld.Keys.ElementAt(0)]);
            }

            return wiFields;
        }

        /// <summary>
        /// Get test Suite Properties
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="PlanId"></param>
        /// <param name="TestSuiteId"></param>
        static void TestSuiteDetails(string TeamProjectName, int TestPlanId, int TestSuiteId, string ParentPath)
        {
            // SuiteExpand.Children does not work in 16.150.0-preview
            TestSuite testSuite = TestPlanClient.GetTestSuiteByIdAsync(TeamProjectName, TestPlanId, TestSuiteId, SuiteExpand.Children).Result;

            PrintSuiteInfo(testSuite, ParentPath);            

            if (testSuite.HasChildren)
                foreach (var suitedef in testSuite.Children)
                    TestSuiteDetails(TeamProjectName, TestPlanId, suitedef.Id, ParentPath + "\\" + testSuite.Name);

            ViewTestCases(TeamProjectName, TestPlanId, testSuite);
            
        }

        /// <summary>
        /// Print info of a test suite
        /// </summary>
        /// <param name="Suite"></param>
        /// <param name="ParentPath"></param>
        static void PrintSuiteInfo(TestSuite Suite, string ParentPath)
        {
            Console.WriteLine("================================================================");
            Console.WriteLine("Test Suite : {0} : {1}", Suite.Id, Suite.Name);

            Console.WriteLine("Suite Type : {0} : {1}", Suite.SuiteType,
                (Suite.SuiteType == TestSuiteType.StaticTestSuite) ? "" :
                (Suite.SuiteType == TestSuiteType.DynamicTestSuite) ? "\nQuery: " + Suite.QueryString : "Requirement ID " + Suite.RequirementId.ToString());
            if (Suite.ParentSuite == null) Console.WriteLine("This is a root suite");
            else Console.WriteLine("Parent Path: " + ParentPath);
            Console.WriteLine("----------------------------------------------------------------");
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
