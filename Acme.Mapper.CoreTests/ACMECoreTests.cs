using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System.Linq;
using acmemapper;

namespace Acme.Mapper.CoreTests
{
    [TestClass, TestCategory("core")]
    public class ACMECoreTests
    {
        protected const string systemA = "systemA";
        protected const string systemB = "systemB";
        protected const string entity = "entity";
        protected const string tosubproperty = "tosubproperty";
        protected const string toarraysubproperty = "toarraysubproperty";
        protected const string property = "property";

        private static acmemapper.Mapper CreateMapper(JObject rules)
        {
            var mapper =  new acmemapper.Mapper(sourceSystem: systemA, destinationSystem: systemB, skipLoadingFromMaps: true);
            mapper.LoadMapping(rules);
            return mapper;
        }

        public JObject _property(JToken propertyValue)
        {
            return new JObject { { "property", propertyValue } };
        }

        public static JObject CreateMappingRule(params JObject[] mappings)
        {
            var rules = new JObject
            {{
                entity, new JArray(mappings)
            }};

            return rules;
        }

        public static V TestCase<T, V>(T input, ref JObject extrafields, params JObject[] mappingRules) where V : new()
        {
            var rules = CreateMappingRule(mappingRules);
            var mapper = CreateMapper(rules);

            return mapper.Map<T, V>(entity, input, ref extrafields);
        }

        public static V TestCase<T,V>(T input, params JObject[] mappingRules) where V:new()
        {
            JObject extrafields = null;
            return TestCase<T, V>(input, ref extrafields, mappingRules);
        }

        [TestMethod]
        public void MappingSimplifiedWithString()
        {
            var output = TestCase<JObject, JObject>(
                mappingRules: new JObject{
                    { systemA, "sourceproperty"} ,
                    { systemB, "destinationproperty" } },
                input: new JObject {
                    { "sourceproperty", "stringvalue" } });

            Assert.AreEqual("stringvalue", output["destinationproperty"].Value<string>());
        }

        [TestMethod]
        public void MappingPartialSimplifiedWithString()
        {
            var output = TestCase<JObject, JObject>(
                mappingRules: new JObject{
                    { systemA, _property("sourceproperty")} ,
                    { systemB, "destinationproperty" } },
                input: new JObject {
                    { "sourceproperty", "stringvalue" } });

            Assert.AreEqual("stringvalue", output["destinationproperty"].Value<string>());
        }

        [TestMethod]
        public void MappingWithString()
        {
            var output = TestCase<JObject,JObject>(
                mappingRules: new JObject{
                    { systemA, _property("sourceproperty")} ,
                    { systemB, _property("destinationproperty") } },
                input: new JObject {
                    { "sourceproperty", "stringvalue" } });

            Assert.AreEqual("stringvalue", output["destinationproperty"].Value<string>());
        }

        [TestMethod, TestCategory("core")]
        public void MappingWithInteger()
        {
            var output = TestCase<JObject, JObject>(
                mappingRules: new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"}} } },
                input: new JObject {
                    { "sourceproperty", 1 } });

            Assert.AreEqual(1, output["destinationproperty"].Value<Int32>());
        }

        [TestMethod]
        public void MappingWithStringMultipleDestinations()
        {
            var output = TestCase<JObject, JObject>(
                /*input:*/ new JObject {
                    { "sourceproperty", "stringvalue" } },
                /*mappingRules:*/ new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty1"}} } },
                   new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty2"}} } });

            Assert.AreEqual("stringvalue", output["destinationproperty1"].Value<string>());
            Assert.AreEqual("stringvalue", output["destinationproperty2"].Value<string>());
        }

        [TestMethod]
        [ExpectedException(typeof(Exception), "property 'destinationproperty' already exists. no subproperty defined for 'systemB'.'destinationproperty'")]
        public void MappingWithStringMultiplesourceSingleDestination()
        {
            TestCase<JObject, JObject>(
                /*input:*/ new JObject {
                    { "sourceproperty1", "stringvalue" },
                    { "sourceproperty2", "stringvalue" } },
                /*mappingRules:*/ new JObject{
                    { systemA, new JObject {{ property, "sourceproperty1"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"}} } },
                new JObject{
                    { systemA, new JObject {{ property, "sourceproperty2"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"}} } });
        }

        [TestMethod]
        public void MappingWithTosubproperty()
        {
            var output = TestCase<JObject, JObject>(
                input: new JObject {
                    { "sourceproperty", "stringvalue" } },
                mappingRules: new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"},
                        { tosubproperty,  "destinationsubproperty"} } } });

            Assert.AreEqual("stringvalue", output["destinationproperty"]["destinationsubproperty"].Value<string>());
        }

        [TestMethod]
        public void MappingWithToArraySubpropertyMerged()
        {
            var output = TestCase<JObject, JObject>(
                    /*input:*/new JObject {
                                  { "sourceproperty1", "stringvalue1" },
                                  { "sourceproperty2", "stringvalue2" } },
                    /*mappingRules:*/ new JObject{
                                          { systemA, new JObject {{ property, "sourceproperty1"} } } ,
                                          { systemB, new JObject {{ property, "destinationproperty"},
                                              { toarraysubproperty, new JObject {
                                                  { systemB, "destinationsubproperty1"},
                                                  { "$action", "merge" } } } } } },
                                      new JObject{
                                          { systemA, new JObject {{ property, "sourceproperty2"} } } ,
                                          { systemB, new JObject {{ property, "destinationproperty"},
                                              { toarraysubproperty, new JObject {
                                                  { systemB, "destinationsubproperty2"},
                                                  { "$action", "merge" } } } } } });

            var array = (output["destinationproperty"] as JArray);

            Assert.AreEqual(1, array.Count);
            Assert.AreEqual(2, array[0].Count());
            Assert.AreEqual("stringvalue1", array[0]["destinationsubproperty1"].Value<string>());
            Assert.AreEqual("stringvalue2", array[0]["destinationsubproperty2"].Value<string>());
        }

        [TestMethod]
        public void MappingWithToArraySubpropertyNonMerged()
        {
            var inputObject = new JObject {
                     { "sourceproperty1", "stringvalue1" },
                     { "sourceproperty2", "stringvalue2" } };

            var output = TestCase<JObject,JObject>(
                inputObject,
                /*mappingRules:*/ new JObject{
                     { systemA, new JObject {{ property, "sourceproperty1"} } },
                     { systemB, new JObject {{ property, "destinationproperty"},
                         { toarraysubproperty, new JObject {
                             { systemB, "destinationsubproperty1"} } } } } },
                 new JObject{
                     { systemA, new JObject {{ property, "sourceproperty2"} } } ,
                     { systemB, new JObject {{ property, "destinationproperty"},
                         { toarraysubproperty, new JObject {
                             { systemB, "destinationsubproperty2"} } } } } }
                    );

            var array = (output["destinationproperty"] as JArray);

            Assert.AreEqual(2, array.Count);
            //TODO we should check the values, should we sort the array as we did in the unit tests?
        }

        [TestMethod]
        public void MappingWithFromsubproperty()
        {
            var output = TestCase<JObject, JObject>(
                input: new JObject {
                    { "sourceproperty", new JObject {
                    { "sourcesubproperty", "stringvalue" } } } },
                mappingRules: new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"},
                        { "fromsubproperty",  "sourcesubproperty"} } } });

            Assert.AreEqual("stringvalue", output["destinationproperty"].Value<string>());
        }

        [TestMethod]
        public void MappingWithCastInteger()
        {
            var output = TestCase<JObject,JObject>(
                input: new JObject {
                    { "sourceproperty", "1" } },
                mappingRules: new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"},
                                              { "type", "System.Int32" } } } });

            // does this test, due to explicit cast of Value<int>() make sense ?
            Assert.AreEqual(1, output["destinationproperty"].Value<int>());
        }

        [TestMethod]
        public void MappingWithMethodInvoke()
        {
            var output = TestCase<JObject,JObject>(
                input: new JObject {
                    { "sourceproperty", "CAPITALLETTER" } },
                mappingRules: new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"},
                                              { "invoke", "ToLowerInvariant" } } } } );
            
            Assert.AreEqual("capitalletter", output["destinationproperty"].Value<string>());
        }

        [TestMethod]
        public void MappingWithMethodInvokeNull()
        {
            var mapper = new acmemapper.Mapper(sourceSystem: "systemA", destinationSystem: "systemB", skipLoadingFromMaps: true);
            var rules = new JObject(
                new JProperty("entity",
                    new JArray(new JObject(
                        new JProperty("systemA",
                            new JObject(new JProperty("property", "sourceproperty"))),
                        new JProperty("systemB",
                            new JObject(
                                new JProperty("property", "destinationproperty"),
                                new JProperty("invoke", "ToLowerInvariant")
                                ))
                         )
                        )));

            mapper.LoadMapping(rules);

            var output = mapper.Map<JObject, JObject>("entity", new JObject(new JProperty("sourceproperty", null)));

            // does this test, due to explicit cast of Value<int>() make sense ?
            Assert.AreEqual(output["destinationproperty"].Type, JTokenType.Null);
        }

        [TestMethod]
        public void MappingWithToSubpropertyAndNullableObject()
        {
            var output = TestCase<JObject,JObject>(
                input: new JObject {
                    { "sourceproperty", null } },
                mappingRules: new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"},
                        { tosubproperty, "destinationsubproperty" } } } } );
            
            Assert.AreEqual(JTokenType.Null, output["destinationproperty"].Type);
        }

        [TestMethod]
        public void MappingWithToSubpropertiesAndNullableObject()
        {
            var output = TestCase<JObject,JObject>(
                /*input:*/ new JObject {
                    { "sourceproperty1", null },
                    { "sourceproperty2", null } },
                /*mappingRules:*/new JObject{
                    { systemA, new JObject {{ property, "sourceproperty1"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"},
                        { tosubproperty, "destinationsubproperty1" } } } },
                new JObject{
                    { systemA, new JObject {{ property, "sourceproperty2"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"},
                        { tosubproperty, "destinationsubproperty2" } } } });

            Assert.AreEqual(JTokenType.Null, output["destinationproperty"].Type);
        }

        [TestMethod]
        public void MappingIgnoreIfNull()
        {
            var output = TestCase<JObject,JObject>(
                input: new JObject{
                    { "sourceproperty", null } },
                mappingRules: new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"},
                        { "ignoreIfNull", true } } } } );

            Assert.AreEqual(null, output["destinationproperty"]);
        }

        [TestMethod]
        public void MappingIgnore()
        {
            var output = TestCase<JObject,JObject>(
                input: new JObject {
                    { "sourceproperty", "stringvalue" } },
                mappingRules: new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"},
                        { "ignore", true } } } } );

            Assert.AreEqual(null, output["destinationproperty"]);
        }

        [TestMethod]
        public void MappingMap()
        {
            var output = TestCase<JObject,JObject>(
                input: new JObject {
                     { "sourceproperty", "1" } },
                mappingRules: new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"},
                        { "map", new JObject {
                            { "1", "stringone"} } } } } } );

            Assert.AreEqual("stringone", output["destinationproperty"].Value<string>());
        }

        [TestMethod]
        public void MappingMapWithEmptyKey()
        {
            var output = TestCase<JObject, JObject>(
                input: new JObject {
                     { "sourceproperty", "" } },
                mappingRules: new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"},
                        { "map", new JObject {
                            { "", "stringone"} } } } } });

            Assert.AreEqual("stringone", output["destinationproperty"].Value<string>());
        }

        [TestMethod]
        public void MappingMapWithDateKey()
        {
            var output = TestCase<JObject, JObject>(
                input: new JObject {
                     { "sourceproperty", "2018-07-30T23:22:25Z" } },
                mappingRules: new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"},
                        { "map", new JObject {
                            { "2018-07-30T23:22:25Z", "stringone"} } } } } });

            Assert.AreEqual("stringone", output["destinationproperty"].Value<string>());
        }

        [TestMethod]
        public void MappingMapDefault()
        {
            var output = TestCase<JObject,JObject>(
                input: new JObject {
                    { "sourceproperty", "nonexpectedvalue" } },
                mappingRules: new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"},
                        { "map", new JObject {
                            { "1", "stringone"},
                            { "$default", "defaultvalue" } } } } } });

            Assert.AreEqual("defaultvalue", output["destinationproperty"].Value<string>());
        }


        [TestMethod]
        public void MappingDefaultValue()
        {
            var output = TestCase<JObject,JObject>(
                input: new JObject {
                    { "sourceproperty", null } } ,
                mappingRules: new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"},{ "default", "defaultvalue" },
                        { "type", "System.String" } } } } );

            Assert.AreEqual("defaultvalue", output["destinationproperty"].Value<string>());
        }

        [TestMethod]
        public void MappingWithUnpecifiedKindDateTime()
        {
            var output = TestCase<JObject,JObject>(
                input: new JObject {
                    { "sourceproperty", "2018-01-01T00:00:00" } },
                mappingRules: new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"},
                        { "type", "System.DateTime" } } } } );

            Assert.AreEqual(new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc), output["destinationproperty"].Value<DateTime>());
        }

        [TestMethod]
        public void MappingToPOCO()
        {
            var output = TestCase<JObject,POCO>(
                input: new JObject {
                    { "sourceproperty", "stringvalue" } },
                mappingRules: new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"} } } });

            Assert.AreEqual("stringvalue", output.destinationproperty);
        }

        [TestMethod]
        public void MappingFromPOCO()
        {
            var output = TestCase<POCO,JObject>(
                input: new POCO
                    { sourceproperty = "stringvalue" },
                mappingRules: new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"} } } });

            Assert.AreEqual("stringvalue", output["destinationproperty"].Value<string>());
        }

        [TestMethod]
        public void MappingToPOCOSubproperty()
        {
            var output = TestCase<JObject,POCO>(
                input: new JObject {
                    { "sourceproperty", "stringvalue" } },
                mappingRules: new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "destination"},
                        { tosubproperty, "destinationsubproperty" } } } } );

            Assert.AreEqual("stringvalue", output.destination.destinationsubproperty);
        }

        [TestMethod]
        public void MappingFromPOCOSubproperty()
        {
            var output = TestCase<POCO,JObject>(
                input: new POCO{ source = new POCOComposition
                    { sourcesubproperty = "stringvalue" } },
                mappingRules: new JObject{
                        { systemA, new JObject {{ property, "source"} } } ,
                        { systemB, new JObject {{ property, "destinationproperty"},
                                                  { "fromsubproperty", "sourcesubproperty"} } } });

            Assert.AreEqual("stringvalue", output["destinationproperty"].Value<string>());
        }

        [TestMethod]
        public void MappingToMissingPOCOproperty()
        {
            var extrafields = new JObject();
            var output = TestCase<JObject, POCO>(
                input: new JObject {
                    { "sourceproperty", "stringvalue" } },
                extrafields: ref extrafields,
                mappingRules: new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "nonexistingpocoproperty"},
                        { tosubproperty, "destinationsubproperty" } } } });

            Assert.AreEqual("stringvalue", extrafields["nonexistingpocoproperty"].Value<string>());
        }

        [TestMethod]
        public void MappingFromMissingPOCOSubproperty()
        {
            var extrafields = new JObject();
            var output = TestCase<POCO, POCO>(
                input: new POCO
                {
                    source = new POCOComposition
                    { sourcesubproperty = "stringvalue" }
                },
                extrafields: ref extrafields,
                mappingRules: new JObject{
                        { systemA, new JObject {{ property, "source"} } } ,
                        { systemB, new JObject {{ property, "destinationmissingPOCOproperty"},
                                                  { "fromsubproperty", "sourcesubproperty"} } } });

            Assert.AreEqual("stringvalue", extrafields["destinationmissingPOCOproperty"].Value<string>());
        }


        [TestMethod]
        public void MappingWithPatternValue()
        {
            var output = TestCase<JObject, JObject>(
                input: new JObject {
                    { "sourceproperty", "stringvalue" } },
                mappingRules: new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "destinationproperty"},
                        { "patternValue", "before_{value}_after" } } } });

            Assert.AreEqual("before_stringvalue_after", output["destinationproperty"].Value<string>());
        }

        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void MappingToMissingPOCOSubpropertyNonExpected()
        {
            var output = TestCase<JObject, POCO>(
                input: new JObject {
                    { "sourceproperty", "stringvalue" } },
                mappingRules: new JObject{
                    { systemA, new JObject {{ property, "sourceproperty"} } } ,
                    { systemB, new JObject {{ property, "nonexistingpocoproperty"},
                        { tosubproperty, "destinationsubproperty" } } } });
        }
    }
}
