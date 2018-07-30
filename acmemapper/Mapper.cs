using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Diagnostics;

namespace acmemapper
{
    /// <summary>
    /// Class for mapping objects based on JSON definition
    /// </summary>
	public class Mapper
    {
        /// <summary>
        /// Source mapping system
        /// </summary>
		public string SourceSystem { get; set; }

        /// <summary>
        /// Get acme.json version
        /// </summary>
		public string MappingVersion { get; internal set; }

        /// <summary>
        /// Destination mapping system
        /// </summary>
		public string DestinationSystem { get; set; }

        /// <summary>
        /// Get entity name
        /// </summary>
		public string Entity { get; set; }
        private JObject jsonrules = null;
        private JArray jsonentity = null;

        /// <summary>
        /// Throw an exception if a warning mapping event occurs
        /// </summary>
		public bool RaiseExceptionOnWarning { get; set; }

        private string jsonschemadirectory = AppDomain.CurrentDomain.BaseDirectory + "/Maps/";

        /// <summary>
        /// Initialize a mapper instance with default settings and Maps loading
        /// </summary>
		public Mapper() : this(false)
        {
        }

        /// <summary>
        /// Initialize a mapper instance with defined source & destination system
        /// </summary>
        /// <param name="sourceSystem"></param>
        /// <param name="destinationSystem"></param>
        public Mapper(string sourceSystem, string destinationSystem) : this(false)
        {
            this.SourceSystem = sourceSystem;
            this.DestinationSystem = destinationSystem;
        }

        /// <summary>
        /// Initialize a mapper instance with defined source & destination system and ability to override Map files
        /// </summary>
        /// <param name="sourceSystem"></param>
        /// <param name="destinationSystem"></param>
        /// <param name="skipLoadingFromMaps"></param>
        public Mapper(string sourceSystem, string destinationSystem, bool skipLoadingFromMaps) : this(skipLoadingFromMaps)
        {
            this.SourceSystem = sourceSystem;
            this.DestinationSystem = destinationSystem;
        }

        /// <summary>
        /// Initiate a mapper instance with control on default rules found on Maps/ folder
        /// </summary>
        /// <param name="skipLoadingFromMaps"></param>
        public Mapper(bool skipLoadingFromMaps)
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                DateTimeZoneHandling = DateTimeZoneHandling.Utc
            };
            this.RaiseExceptionOnWarning = false;

            this.jsonrules = new JObject();

            if (!skipLoadingFromMaps)
            {
                foreach (var entityfile in System.IO.Directory.GetFiles(jsonschemadirectory, "*.json"))
                {
                    var entityjson = JObject.Parse(System.IO.File.ReadAllText(entityfile));
                    var entity = entityjson.Children<JProperty>().First(x => x.Name != "$version");
                    this.jsonrules.Add(entity.Name, entity.Value);

                    // TODO we use the last version we found
                    this.MappingVersion = entityjson["$version"].Value<String>();
                }
            }
        }

        /// <summary>
        /// Load mapping rules directly from JObject
        /// </summary>
        /// <param name="rules"></param>
        public void LoadMapping(JObject rules)
        {
            // TODO json schema check ?
            if (rules != null)
                this.jsonrules = rules;
        }

        /// <summary>
        /// Define current Entity
        /// </summary>
        /// <param name="entityName"></param>
		internal void SetEntity(string entityName)
        {
            jsonentity = (JArray)this.jsonrules[entityName];
            if (jsonentity == null)
            {
                throw new Exception(String.Format("Entity name '{0}' not found in directory {1}", entityName, jsonschemadirectory));
            }
            this.Entity = entityName;
        }

        public D Map<S, D>(string entityName, S source, ref JObject extrafields) where D : new()
        {
            this.SetEntity(entityName);
            return Map<S, D>(source, ref extrafields);
        }

        /// <summary>
        /// Map object
        /// </summary>
        /// <typeparam name="S">Source type</typeparam>
        /// <typeparam name="D">Destination type</typeparam>
        /// <param name="entityName">Entity</param>
        /// <param name="source">Source object</param>
        /// <returns>Mapped object</returns>
		public D Map<S, D>(string entityName, S source) where D : new()
        {
            this.SetEntity(entityName);
            JObject extrafields = null;
            return Map<S, D>(source, ref extrafields);
        }

        /// <summary>
        /// Map an input object with already defined Entity
        /// </summary>
        /// <typeparam name="S">Source type</typeparam>
        /// <typeparam name="D">Destination type</typeparam>
        /// <param name="source">Source object</param>
        /// <param name="extrafields">Unmapped fields for POCO destination</param>
        /// <returns>Mapped object</returns>
        public D Map<S, D>(S source, ref JObject extrafields) where D : new()
        {

            if (jsonentity == null)
                throw new Exception("No entity has been defined");

            var newObj = new D();
            var sourcejson = JObject.Parse(JsonConvert.SerializeObject(source));
            var destinationjson = JObject.Parse(JsonConvert.SerializeObject(newObj));

            // and now the magic 
            foreach (var property in sourcejson)
            {
                var mappingrules = jsonentity.Where(x =>
                    x.Value<JObject>(this.SourceSystem) != null &&
                    x.Value<JObject>(this.DestinationSystem) != null &&
                    x.Value<JObject>(this.SourceSystem)["property"].Value<string>() == property.Key);

                if (mappingrules.Count() == 0)
                {
                    string message = String.Format("Mapping for {0} not found in entity {1} from {2} to {3}", property.Key, this.Entity, this.SourceSystem, this.DestinationSystem);

                    if (this.RaiseExceptionOnWarning)
                        throw new Exception(message);
                    else
                    {
                        //Trace.TraceWarning(message);
                        continue;
                    }
                }

                foreach (var mappingrule in mappingrules)
                {
                    if (mappingrule[this.DestinationSystem]["ignore"] != null && mappingrule[this.DestinationSystem]["ignore"].Value<bool>() == true)
                        continue;

                    if (mappingrule[this.DestinationSystem]["ignoreIfNull"] != null &&
                        mappingrule[this.DestinationSystem]["ignoreIfNull"].Value<bool>() == true &&
                        property.Value.Type == JTokenType.Null)
                        continue;

                    JToken value = null;

                    // if our input property is a nested object (rather a single property), we might need to crawl a little bit more
                    if (property.Value.Count() == 0 || property.Value.Type == JTokenType.Array)
                        value = property.Value;
                    else
                    {
                        string sourceSubproperty = mappingrule.SelectToken(this.DestinationSystem)?.SelectToken("fromsubproperty")?.Value<string>();
                        if (sourceSubproperty == null)
                        {
                            string message = String.Format("missing 'fromsubproperty' for destination system '{0}' for rule {1}", this.DestinationSystem, mappingrule.ToString().Replace(Environment.NewLine, String.Empty));
                            //Trace.TraceError(message);
                            throw new Exception(message);
                        }
                        else
                        {
                            if (property.Value[sourceSubproperty] != null)
                                value = property.Value[sourceSubproperty];
                            else
                                continue; // the property field do not exist in property.Value
                        }
                    }

                    // null values should not be proceed as nested destination type
                    string destinationpropertyname = mappingrule.SelectToken(this.DestinationSystem)?.SelectToken("property")?.Value<string>();
                    string destinationSubproperty = mappingrule.SelectToken(this.DestinationSystem)?.SelectToken("tosubproperty")?.Value<string>();
                    string destinationSubpropertyType = mappingrule.SelectToken(this.DestinationSystem)?.SelectToken("type")?.Value<string>();
                    JToken destinationSubpropertyTypeDefaultValue = mappingrule.SelectToken(this.DestinationSystem)?.SelectToken("default");
                    string destinationInvoke = mappingrule.SelectToken(this.DestinationSystem)?.SelectToken("invoke")?.Value<string>();
                    JToken destinationTransformations = mappingrule.SelectToken(this.DestinationSystem)?.SelectToken("map");
                    string patternValue = mappingrule.SelectToken(this.DestinationSystem)?.SelectToken("patternValue")?.Value<string>();

                    string destinationArraySubproperty = mappingrule.SelectToken(this.DestinationSystem)?.SelectToken("toarraysubproperty")?.SelectToken(this.DestinationSystem)?.Value<string>();


                    if (destinationSubpropertyType != null)
                    {
                        // if we are in a cast situation, but getting a null or empty string in input, return null
                        if (property.Value.Type == JTokenType.Null || (property.Value.Type == JTokenType.String && String.IsNullOrEmpty(property.Value.Value<string>())))
                        {
                            if (destinationSubpropertyTypeDefaultValue != null)
                                value = destinationSubpropertyTypeDefaultValue;
                            else
                                value = null;
                        }
                        else
                        {
                            try
                            {
                                value = JToken.FromObject(value.ToObject(Type.GetType(destinationSubpropertyType)));
                            }
                            catch (Exception ex) when (ex is FormatException || ex is JsonReaderException || ex is JsonSerializationException)
                            {
                                if (destinationSubpropertyTypeDefaultValue != null)
                                    value = destinationSubpropertyTypeDefaultValue;
                                else
                                {
                                    string message = String.Format("incorrect cast {0} for property '{1}':'{2}'  for rule {3}", destinationSubpropertyType, property.Key, property.Value, mappingrule.ToString().Replace(Environment.NewLine, String.Empty));
                                    throw new Exception(message);
                                }
                            }
                        }
                    }

                    /*** apply transformations ***/
                    // invoke methods
                    if (!String.IsNullOrEmpty(destinationInvoke))
                        value = this.ApplyInvokeTransformation(destinationInvoke, value);

                    // transform value
                    if (destinationTransformations != null && value != null)
                        value = this.ApplyValueTransformation(destinationTransformations, value);

                    // Format value with defined Pattern
                    if (patternValue != null)
                        value = this.ApplyValueRework(patternValue, value);

                    // if destination object is a "free one" like JObject, we do not check destination mapping
                    if (newObj is JObject)
                    {
                        // are we in a subproperty/nested destination scenario
                        if (destinationpropertyname == null)
                        {
                            string message = String.Format("No reference of destination system {0} for rule {1}", this.DestinationSystem, mappingrule.ToString().Replace(Environment.NewLine, String.Empty));
                            throw new Exception(message);
                        }

                        JToken destinationPropertyToken = destinationjson[destinationpropertyname];

                        //TODO  AddProperties (we could rework the logic to use a "target" JToken rather than destinationjson[destinationproperty] to preserve existent logic
                        if (destinationArraySubproperty != null)
                        {
                            bool merge = mappingrule.SelectToken(this.DestinationSystem)?.SelectToken("toarraysubproperty")?.SelectToken("$action")?.Value<string>().ToLowerInvariant() == "merge";

                            //create if not exists, apply merge logic
                            if (destinationjson[destinationpropertyname] == null)
                            {
                                //create new JArray if not exists with first property
                                destinationjson[destinationpropertyname] = new JArray
                                    {
                                        new JObject(new JProperty(destinationArraySubproperty, value))
                                    };
                                destinationPropertyToken = (destinationjson[destinationpropertyname] as JArray)[0];
                            }
                            else
                            {
                                if (merge)
                                {
                                    //we always point to the single entry in the array (we merge the properties in it)
                                    destinationjson[destinationpropertyname][0][destinationArraySubproperty] = value;
                                    destinationPropertyToken = (destinationjson[destinationpropertyname] as JArray)[0];
                                }
                                else
                                {
                                    //create new element always and point to it (the objective is to have one item for each property mapped to this array)
                                    (destinationjson[destinationpropertyname] as JArray)
                                        .Add(new JObject(new JProperty(destinationArraySubproperty, value)));
                                    destinationPropertyToken = (destinationjson[destinationpropertyname] as JArray).Last;
                                }
                            }
                        }
                        else
                        {
                            if (destinationPropertyToken == null)
                            {
                                if (destinationSubproperty != null)
                                    destinationjson.Add(destinationpropertyname, new JObject(new JProperty(destinationSubproperty, value)));
                                else
                                    destinationjson.Add(destinationpropertyname, value);
                            }
                            else
                            {
                                if (destinationSubproperty != null)
                                {
                                    if (destinationjson[destinationpropertyname].Type == JTokenType.Object && destinationjson[destinationpropertyname][destinationSubproperty] == null)
                                        ((JObject)destinationjson[destinationpropertyname]).Add(new JProperty(destinationSubproperty, value));
                                    else if (destinationjson[destinationpropertyname].Type == JTokenType.Null)
                                        destinationjson[destinationpropertyname] = new JObject(new JProperty(destinationSubproperty, value));
                                    else
                                    {
                                        if (destinationjson[destinationpropertyname]?.SelectToken(destinationSubproperty) == null)
                                        {
                                            throw new Exception(String.Format("Tried to insert subproperty '{0}' into property '{1}' but not nested object. Found '{2}'", destinationSubproperty, destinationpropertyname, destinationjson[destinationpropertyname]));
                                        }
                                        else
                                        {
                                            string message = String.Format("Destination '{0}' property already filled in. Duplicate detected '{1}'", destinationpropertyname, property.Value.ToString().Replace(Environment.NewLine, String.Empty));
                                            if (this.RaiseExceptionOnWarning)
                                                throw new Exception(message);
                                            else
                                                Trace.TraceWarning(message);
                                        }
                                    }
                                }
                                else
                                {
                                    string message = String.Format("property '{0}' already exists. no subproperty defined for '{1}'.'{0}'", destinationpropertyname, this.DestinationSystem);
                                    Trace.TraceError(message);
                                    throw new Exception(message);
                                }
                            }

                            destinationPropertyToken = destinationjson[destinationpropertyname];
                        }

                        // extra properties (mainly for flagging lookup)
                        if (mappingrule.SelectToken(this.DestinationSystem)?.SelectToken("addproperties") != null)
                            foreach (var addproperty in mappingrule.SelectToken(this.DestinationSystem)?.SelectToken("addproperties")?.Children())
                            {
                                if (destinationPropertyToken.GetType() == typeof(JValue))
                                {
                                    string message = String.Format("Wrong JValue type to add property {0}", mappingrule.ToString().Replace(Environment.NewLine, String.Empty));
                                    throw new Exception(message);
                                }
                                else if (destinationPropertyToken[((JProperty)addproperty).Name] == null) // duplicate declaration could happen, we have to check first
                                    ((JObject)destinationPropertyToken).Add(addproperty);
                            }
                    }
                    else
                    {
                        var destinationpropertyvalue = destinationjson[destinationpropertyname];

                        if (destinationpropertyvalue == null)
                        {
                            if (extrafields == null)
                            {
                                string message = String.Format("Invalid destination POCO property {4} mapping for {0} not found in entity {1} from {2} to {3}", property.Key, this.Entity, this.SourceSystem, this.DestinationSystem, destinationpropertyname);
                                throw new Exception(message);
                            }
                            else
                                extrafields.Add(new JProperty(destinationpropertyname, value));
                        }
                        else
                        {
                            if (destinationSubproperty == null)
                                destinationjson[destinationpropertyname] = value;
                            else
                                destinationjson[destinationpropertyname][destinationSubproperty] = value;
                        }

                    }
                }
            }

            return ApplyNullNestedNode(destinationjson).ToObject<D>();
        }

        /// <summary>
        /// Rework output property as null if all sub properties are null
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
		private JObject ApplyNullNestedNode(JObject input)
        {
            // loop on all nested nodes and if all values are null or empty (for string) (except specific keyword such "lookup"
            // the nested node will be transformed to normal JToken with null value
            var keywordException = new[] { "lookup" };
            foreach (var node in input.Children<JProperty>().Where(x => x.Value.Type == JTokenType.Object))
            {
                var any = node.Value.Children<JProperty>().Any(x => !keywordException.Contains(x.Name) &&
                    x.Value.Type != JTokenType.Null &&
                    !String.IsNullOrEmpty(x.Value.Value<String>()));

                // no "normal" fields, only keyword or null or empty
                if (!any)
                    node.Value = null;
            }

            return input;
        }

        /// <summary>
        /// Perform a C# reflection action on destination type.
        /// </summary>
        /// <param name="invokeMethod">Method which will be called by reflection</param>
        /// <param name="input">value JToken</param>
        /// <returns></returns>
        private JToken ApplyInvokeTransformation(string invokeMethod, JToken input)
        {
            if (input.Type == JTokenType.Null)
                return null;

            var type = "System." + input.Type.ToString();
            var destinationtype = Type.GetType(type, false);
            if (destinationtype == null)
                throw new Exception($" InvokeTransformation can't find Type '{type}'");

            // ping invokeMethod with only signature without input parameters
            var transformationmethod = destinationtype.GetMethod(invokeMethod, new Type[] { });
            if (transformationmethod == null)
                throw new Exception($" InvokeTransformation can't find method '{invokeMethod}' for type '{type}'");

            var output = transformationmethod.Invoke(Convert.ChangeType(input, destinationtype), null);

            return JToken.FromObject(output);
        }

        /// <summary>
        /// Perform a C# reflection action on destination type.
        /// </summary>
        /// <param name="map">Method which will be called by reflection</param>
        /// <param name="input">value JToken</param>
        /// <returns></returns>
        private JToken ApplyValueTransformation(JToken map, JToken input)
        {
            var inputkey = input.Value<string>();
            var output = map?.SelectToken(inputkey);
            if (map != null && output == null)
            {
                var defaultToken = map.SelectToken("$default");
                if (defaultToken != null)
                {
                    output = defaultToken;
                }
                else
                {
                    output = input;
                }

            }

            return output;
        }

        /// <summary>
        /// Perform a C# replace action in order to customize de value output.
        /// </summary>
        /// <param name="pattern">extra content added to the value, sample : prefix{value}suffix</param>
        /// <param name="input">value JToken</param>
        /// <returns></returns>
        private string ApplyValueRework(string pattern, JToken input)
        {
            var result = pattern.Replace("{value}", input.ToString());
            return result;
        }
    }
}
