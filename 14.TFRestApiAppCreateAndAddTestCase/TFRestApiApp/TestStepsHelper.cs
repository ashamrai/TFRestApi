using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFRestApiApp
{
    
    class LocalStepsDefinition
    {
        class LocalTestStep
        {
            private const string StepContainerStr = @"<step type=""{0}"" id=""{1}""><parameterizedString isformatted=""true"">{2}</parameterizedString><parameterizedString isformatted=""true"">{3}</parameterizedString></step>";
            public string Action { get; set; }
            public string Validation { get; set; }

            /// <summary>
            /// Format string for the current step
            /// </summary>
            /// <param name="StepIndex">index in the steps order (+2 to the index in the steps list)</param>
            /// <returns></returns>
            public string StepStr(int StepIndex)
            {
                string StepType = "ActionStep";
                if (Validation != null)
                    StepType = "ValidateStep";

                return String.Format(StepContainerStr, StepType, StepIndex, Action, Validation);
            }
        }


        private const string StepsContainerStr = @"<steps id=""0"" last=""{0}"">{1}</steps>";

        private List<LocalTestStep> localTestSteps = new List<LocalTestStep>();

        public void AddStep(string Action, string Validation = null)
        {
            localTestSteps.Add(new LocalTestStep { Action = Action, Validation = Validation });
        }

        public int StepCount
        {
            get { return localTestSteps.Count; }
        }

        /// <summary>
        /// Format string for all steps (Field: Microsoft.VSTS.TCM.Steps)
        /// </summary>
        public string StepsDefinitionStr
        {
            get
            {
                if (localTestSteps.Count == 0) return null;

                string stepsStr = "";

                //add definition for each step
                for (int i = 0; i < localTestSteps.Count; i++) stepsStr += localTestSteps[i].StepStr(i + 2);

                return String.Format(StepsContainerStr, localTestSteps.Count + 1, stepsStr);
            }
        }
    }

    class LocalTestParams
    {
        private const string ParamDataSourceContainerStr = @"<NewDataSet>{0}{1}{2}{3}</NewDataSet>";
        private const string XSSchemaStrStart = @"<xs:schema id='NewDataSet' xmlns:xs='http://www.w3.org/2001/XMLSchema' xmlns:msdata='urn:schemas-microsoft-com:xml-msdata'><xs:element name='NewDataSet' msdata:IsDataSet='true' msdata:Locale=''><xs:complexType><xs:choice minOccurs='0' maxOccurs = 'unbounded'><xs:element name='Table1'><xs:complexType><xs:sequence>";
        private const string XSSchemaStrEnd = @"</xs:sequence></xs:complexType></xs:element></xs:choice></xs:complexType></xs:element></xs:schema>";
        private const string XSParamStrConatiner = @"<xs:element name='{0}' type='xs:string' minOccurs='0' />";
        private const string DSParamTableStrContainer = @"<Table1>{0}</Table1>";
        private const string DSParamTableParamStrContainer = @"<{0}>{1}</{0}>";
        private const string ParametersDefinitionStrContainer = @"<parameters>{0}</parameters>";
        private const string ParameterDefinitionStrContainer = @"<param name=""{0}"" bind=""default""/>";

        private Dictionary<string, string[]> ParamValues = new Dictionary<string, string[]>();

        public int ParamCount
        {
            get { return ParamValues.Count; }
        }

        public void AddParam(string Name, string[] Values)
        {
            ParamValues.Add(Name, Values);
        }

        /// <summary>
        /// Format string for parameters definition (Field: Microsoft.VSTS.TCM.Parameters)
        /// </summary>
        public string ParamDefinitionStr
        {
            get
            {
                if (ParamValues.Count == 0) return null;

                string parameters = "";

                foreach (var param in ParamValues) parameters += String.Format(ParameterDefinitionStrContainer, param.Key);

                return String.Format(ParametersDefinitionStrContainer, parameters);
            }
        }

        /// <summary>
        /// Format string for the table with values
        /// </summary>
        private string ParamValuesStr
        {
            get
            {
                if (ParamValues.Count == 0) return null;

                int paramValuesCount = ParamValues.ElementAt(0).Value.Length; //just get the count from the first parameter;

                string tableRowStr = "";
                
                for (int i = 0; i < paramValuesCount; i++)
                {
                    string tableRowParams = "";

                    foreach (var param in ParamValues)
                        if (i < param.Value.Length)
                            tableRowParams += String.Format(DSParamTableParamStrContainer, param.Key, param.Value[i]); //add parameter value for the iteration i

                    tableRowStr += String.Format(DSParamTableStrContainer, tableRowParams);
                }

                return tableRowStr;
            }
        }

        /// <summary>
        /// Format string for the definition in the schema 
        /// </summary>
        private string ParamsSchemaDefStr
        {
            get
            {
                if (ParamValues.Count == 0) return null;

                string parameters = "";

                foreach (var param in ParamValues) parameters += String.Format(XSParamStrConatiner, param.Key);

                return parameters;
            }
        }

        /// <summary>
        /// Form string for parameters values (Field: Microsoft.VSTS.TCM.LocalDataSource)
        /// </summary>
        public string ParamDataSetStr
        {
            get
            {
                return String.Format(ParamDataSourceContainerStr, XSSchemaStrStart, ParamsSchemaDefStr, XSSchemaStrEnd, ParamValuesStr);
            }
        }
    }
}
