using System.Text.Json;
using System.Text.Json.Nodes;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Serialization.Json;
using MyFhirSdk.Types;

return PatientSimpleJsonFixtureMatchesSerializer();

static int PatientSimpleJsonFixtureMatchesSerializer()
{
    var serializer = new FhirJsonSerializer();
    var actualJson = serializer.Serialize(CreateSimplePatient());
    var expectedJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "patient-simple.json"));

    var normalizedExpected = NormalizeJson(expectedJson);
    var normalizedActual = NormalizeJson(actualJson);

    if (normalizedExpected == normalizedActual)
    {
        Console.WriteLine("PASS patient-simple.json matches serialized Patient resource.");
        return 0;
    }

    Console.Error.WriteLine("FAIL patient-simple.json does not match serialized Patient resource.");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Expected:");
    Console.Error.WriteLine(normalizedExpected);
    Console.Error.WriteLine();
    Console.Error.WriteLine("Actual:");
    Console.Error.WriteLine(normalizedActual);
    return 1;
}

static Patient CreateSimplePatient()
{
    return new Patient
    {
        Id = "patient-simple",
        Active = new FhirBoolean(true),
        Gender = new FhirCode("male"),
        BirthDate = new FhirDate("1974-12-25"),
        Identifier =
        {
            new Identifier
            {
                System = new FhirUri("http://hospital.example.org/patients"),
                Value = new FhirString("MRN-12345")
            }
        },
        Name =
        {
            new HumanName
            {
                Use = new FhirCode("official"),
                Family = new FhirString("Chalmers"),
                Given =
                {
                    new FhirString("Peter"),
                    new FhirString("James")
                }
            }
        },
        Telecom =
        {
            new ContactPoint
            {
                System = new FhirCode("phone"),
                Value = new FhirString("555-0100"),
                Use = new FhirCode("home")
            }
        },
        Address =
        {
            new Address
            {
                Use = new FhirCode("home"),
                Line =
                {
                    new FhirString("534 Erewhon St")
                },
                City = new FhirString("PleasantVille"),
                State = new FhirString("Vic"),
                PostalCode = new FhirString("3999")
            }
        }
    };
}

static string NormalizeJson(string json)
{
    var node = JsonNode.Parse(json) ?? throw new InvalidOperationException("JSON payload was empty.");
    var normalized = NormalizeNode(node);

    return normalized.ToJsonString(new JsonSerializerOptions
    {
        WriteIndented = true
    });
}

static JsonNode NormalizeNode(JsonNode node)
{
    if (node is JsonObject jsonObject)
    {
        var normalizedObject = new JsonObject();

        foreach (var property in jsonObject.OrderBy(property => property.Key, StringComparer.Ordinal))
        {
            normalizedObject[property.Key] = property.Value is null
                ? null
                : NormalizeNode(property.Value);
        }

        return normalizedObject;
    }

    if (node is JsonArray jsonArray)
    {
        var normalizedArray = new JsonArray();

        foreach (var item in jsonArray)
        {
            normalizedArray.Add(item is null ? null : NormalizeNode(item));
        }

        return normalizedArray;
    }

    return node.DeepClone();
}
