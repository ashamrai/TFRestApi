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
        static readonly string UserPAT = "";

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

                string teamProjectName = "Team Project Name";
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
            TestPlan testPlan = TestManagementClient.GetPlanByIdAsync(TeamProjectName, TestPlanId).Result;

            Console.WriteLine("================================================================");
            Console.WriteLine("Test Plan  : {0} : {1} : {2}", testPlan.Id, testPlan.State, testPlan.Name);
            Console.WriteLine("Area Path  : {0} : Iteration Path : {1}", testPlan.Area.Name, testPlan.Iteration);
            Console.WriteLine("Plan Dates : {0} - {1}", testPlan.StartDate.ToShortDateString(), testPlan.EndDate.ToShortDateString());

            int rootsuiteId = 0;            

            if (int.TryParse(testPlan.RootSuite.Id, out rootsuiteId))
            {
                TestSuiteDetails(TeamProjectName, testPlan.Id, testPlan.RootSuite.Id);
            }
        }

        /// <summary>
        /// Get test Suite Properties
        /// </summary>
        /// <param name="TeamProjectName"></param>
        /// <param name="PlanId"></param>
        /// <param name="TestSuiteId"></param>
        static void TestSuiteDetails(string TeamProjectName, int PlanId, string TestSuiteId)
        {
            int suiteId = 0;

            if (!int.TryParse(TestSuiteId, out suiteId)) return;

            TestSuite suiteDetail = TestManagementClient.GetTestSuiteByIdAsync(TeamProjectName, PlanId, suiteId, 1).Result;

            Console.WriteLine("================================================================");
            Console.WriteLine("Test Suite : {0} : {1} : {2}", suiteDetail.Id, suiteDetail.State, suiteDetail.Name);

            //Sute Types: StaticTestSuite, RequirementTestSuite, DynamicTestSuite            
            Console.WriteLine("Suite Type : {0} : {1}", suiteDetail.SuiteType,
                (suiteDetail.SuiteType == "StaticTestSuite") ? "" :
                (suiteDetail.SuiteType == "DynamicTestSuite") ? "\nQuery: " + suiteDetail.QueryString : "Requirement ID " + suiteDetail.RequirementId.ToString());
            if (suiteDetail.Parent == null) Console.WriteLine("This is a root suite");
            Console.WriteLine("----------------------------------------------------------------");

            if (suiteDetail.Suites != null && suiteDetail.Suites.Count > 0)
                foreach (var suitedef in suiteDetail.Suites)
                    TestSuiteDetails(TeamProjectName, PlanId, suitedef.Id);

            if (suiteDetail.TestCaseCount > 0)
            {
                //get test cases info

                List<SuiteTestCase> testCases = TestManagementClient.GetTestCasesAsync(TeamProjectName, PlanId, suiteId).Result;

                foreach(SuiteTestCase testCase in testCases)
                {
                    int testId = 0;

                    if (!int.TryParse(testCase.Workitem.Id, out testId)) continue;

                    WorkItem wi = WitClient.GetWorkItemAsync(testId).Result;

                    Console.WriteLine("Test: {0} - {1}", wi.Id, wi.Fields["System.Title"].ToString());
                    foreach(var config in testCase.PointAssignments)
                        Console.WriteLine("Run for: {0} : {1}", config.Tester.DisplayName, config.Configuration.Name);

                }
            }
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
