using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using NJsonSchema;
using acmemapper;

namespace Acme.Mapper.Tests
{
	[TestClass]
	public class MapperUnitTests
	{
		static acmemapper.Mapper mapper = null;
        static Dictionary<string, JsonSchema4> schemas = new Dictionary<string, JsonSchema4>();

        [ClassInitialize]
		public static void TestClassinitialize(TestContext context)
		{
			mapper = new acmemapper.Mapper();

            // load json schema files
            foreach (var schema in System.IO.Directory.GetFiles(@"schema", "*.json"))
            {
                var fileinfo = new System.IO.FileInfo(schema);
                schemas.Add(fileinfo.Name.Replace(fileinfo.Extension, String.Empty), JsonSchema4.FromFileAsync(schema).Result);
            }
        }

		//[TestMethod, TestCategory("Mapping")]
		//public void AcmeEmployeeCrmEmployeeContact()
		//{
		//	string message = string.Empty;
		//	if (!CompareInputOutputJSON(
		//		sourcesystem: "acme",
		//		sourcejsonpath: @"json\AcmeEmployee_input.json",
		//		destinationsystem: "crm",
		//		destinationjsonpath: @"json\AcmeEmployeeCrmEmployeeContact_output.json",
		//		entity: "employee.contact",
        //      checkSchema: true,
		//		message: out message))
		//		Assert.Fail(message);
		//}
        
        /// <summary>
        ///
        /// </summary>
        /// <param name="sourcesystem">source system</param>
        /// <param name="sourcejsonpath">input json</param>
        /// <param name="destinationsystem">destination system</param>
        /// <param name="destinationjsonpath">expected output json</param>
        /// <param name="entity">mapped entity</param>
        /// <param name="message">output warning message</param>
        /// <param name="checkSchema">JSON schema validation on result</param>
        /// <returns></returns>
        private bool CompareInputOutputJSON(string sourcesystem, string sourcejsonpath, string destinationsystem, string destinationjsonpath, string entity, out string message, bool checkSchema = true)
		{
			mapper.SourceSystem = sourcesystem;
			mapper.DestinationSystem = destinationsystem;
			var reference = mapper.DestinationSystem + "." + entity;
			var input = JObject.Parse(File.ReadAllText(sourcejsonpath));
			var expected_ouput = JObject.Parse(File.ReadAllText(destinationjsonpath));
            var output = mapper.Map<JObject, JObject>(entity, input);

			message = string.Empty;

            var successful = true;
            successful &= EqualJSON(expected_ouput, output, out message);

            // if failing, let's drop the actual output for easier debugging
            if (!successful)
                File.WriteAllText(reference + ".json", output.ToString());

            if (!checkSchema)
                return successful;

            // json schema validation
            // only acme json is checked, either input or output
            // for 1 ... N system mapping : ACME employee -> CRM systemuser & CRM contact
            // entity name after "." is ignored
            var entityname = entity.Contains('.') ? entity.Substring(0, entity.IndexOf('.')) : entity;
            var schema = schemas[entityname];
            var acmeerrors = schema.Validate(sourcesystem == "acme" ? input : output);
            if (acmeerrors.Count > 0)
            {
                successful &= false;
                message += schema.DocumentPath + " " + schema.ExtensionData["version"].ToString() + Environment.NewLine;
                message += string.Join(Environment.NewLine, acmeerrors.Select(s=> s.ToString()).ToArray());
            }

            return successful;
		}

        private JArray sort(JArray input)
        {
            var comparer = new JTokenEqualityComparer();
            return new JArray(input.OrderBy((item) => comparer.GetHashCode(item)));
        }

        private void SortArrays(JObject input)
        {
            foreach (var property in input)
            {
                if (property.Value.Type == JTokenType.Array)
                {
                    input[property.Key] = sort(property.Value as JArray);
                }
            }
        }

        bool EqualJSON(JObject source, JObject target, out string message)
		{
            SortArrays(source);
            SortArrays(target);

			message = "acme.json version : " + mapper.MappingVersion + Environment.NewLine;
			if (!JToken.DeepEquals(target, source))
			{
				foreach (KeyValuePair<string, JToken> sourceProperty in source)
				{
					JProperty targetProp = target.Property(sourceProperty.Key);

					if (targetProp == null)
						message += sourceProperty.Key + " missing " + Environment.NewLine;
					else if (!JToken.DeepEquals(sourceProperty.Value, targetProp.Value))
						message += sourceProperty.Key + " changed " + Environment.NewLine;
				}

				foreach (KeyValuePair<string, JToken> targetProperty in target)
				{
					JProperty sourceProp = source.Property(targetProperty.Key);

					if (sourceProp == null)
						message += targetProperty.Key + " unexpected  " + Environment.NewLine;
					else if (!JToken.DeepEquals(targetProperty.Value, sourceProp.Value))
						message += targetProperty.Key + " changed " + Environment.NewLine;
				}

				return false;
			}
			return true;
		}

	}
}