[![Nuget](https://img.shields.io/nuget/v/acmemapper.svg)](https://www.nuget.org/packages/acmemapper)

# ACMEMAPPER

`ACMEMAPPER` has been developed focusing on mapping definition maintainability & lisibility over flexibility & performance: mapping definitions are declared in JSON and reloaded on each new `ACMEMAPPER` object. Many source or destination objects type are supported such `JObject` or any `POCO` ones.
`ACMEMAPPER` has been initiality developed in context of an Enterprise Service Bus which has to support & manage many mapping activities and update mapping definitions without re-build & deploy.

`ACMEMAPPER` supports by design several entity definitions and no limitation of source & destination systems (including both way mapping). By design, `ACMEMAPPER` supports only as input 2 levels of data (either JSON or POCO object)
```json
{
    "level1field" : "value1",
    "nestedobject" : {
        "level2field" : "subvalue2"
    }
}
```

## Simple mapping example

Mapping definition
```json
{
    "$version" : "1.0",
    "myobjecttype" : [
        {
            "systemA" : {
                "property" : "systemAfield1"
            },
            "systemB" : {
                "property" : "systemBfield1"
            }
        },
        {
            "systemA" : {
                "property" : "systemAfield2"
            },
            "systemB" : {
                "property" : "systemBfield2"
            }
        }
    ]
}
```

C# source code with 
```csharp
    using acmemapper;
    ...
    var mapper = new Mapper("systemA", "systemB");
    var output = mapper.Map<JObject,JObject>("myobjecttype",new JObject { { "systemAfield1" : "mystringvalue" } });
```

JObject/JSON output
```json
{
    "systemBfield1" : "mystringvalue"
}
```

## HOW TO
* [First mapping in 5 minutes](https://github.com/camous/acmemapper/wiki/HOW-TO-:-First-mapping-in-Visual-Studio)

## Features

* [Nested JSON content mapping](https://github.com/camous/acmemapper/wiki/Nested-objects)
* 1...N mapping definition
* POCO basic object composition
* Control flags : `Ignore` / `IgnoreIfNull`
* Basic casting
* Basic transformation : switch (`map`)
* Basic method invokation (eg. `ToLowerInvariant`)

