using MyFhirSdk.Resources;

internal static class PatientJsonParserTestCases
{
    public static IReadOnlyList<JsonParserTestCase> All { get; } =
    [
        new JsonParserTestCase(
            Path.Combine("Resources", "patient-simple.json"),
            "Patient",
            AssertSimplePatient),
        new JsonParserTestCase(
            Path.Combine("Resources", "patient-list-name.json"),
            "Patient",
            AssertPatientWithListName)
    ];

    private static void AssertSimplePatient(FhirJsonParser parser, string json)
    {
        var patient = parser.Parse<Patient>(json);

        ParserAssert.Equal("patient-simple", patient.Id, "patient.Id");
        ParserAssert.Equal(true, ParserAssert.NotNull(patient.Active, "patient.Active").Value, "patient.Active.Value");
        ParserAssert.Equal("male", ParserAssert.NotNull(patient.Gender, "patient.Gender").Value, "patient.Gender.Value");
        ParserAssert.Equal("1974-12-25", ParserAssert.NotNull(patient.BirthDate, "patient.BirthDate").Value, "patient.BirthDate.Value");

        ParserAssert.Count(1, patient.Identifier, "patient.Identifier");
        var identifier = patient.Identifier[0];
        ParserAssert.Equal(
            "http://hospital.example.org/patients",
            ParserAssert.NotNull(identifier.System, "patient.Identifier[0].System").Value,
            "patient.Identifier[0].System.Value");
        ParserAssert.Equal(
            "MRN-12345",
            ParserAssert.NotNull(identifier.Value, "patient.Identifier[0].Value").Value,
            "patient.Identifier[0].Value.Value");

        ParserAssert.Count(1, patient.Name, "patient.Name");
        var name = patient.Name[0];
        ParserAssert.Equal("official", ParserAssert.NotNull(name.Use, "patient.Name[0].Use").Value, "patient.Name[0].Use.Value");
        ParserAssert.Equal("Chalmers", ParserAssert.NotNull(name.Family, "patient.Name[0].Family").Value, "patient.Name[0].Family.Value");
        ParserAssert.Count(2, name.Given, "patient.Name[0].Given");
        ParserAssert.Equal("Peter", name.Given[0].Value, "patient.Name[0].Given[0].Value");
        ParserAssert.Equal("James", name.Given[1].Value, "patient.Name[0].Given[1].Value");

        ParserAssert.Count(1, patient.Telecom, "patient.Telecom");
        var telecom = patient.Telecom[0];
        ParserAssert.Equal("phone", ParserAssert.NotNull(telecom.System, "patient.Telecom[0].System").Value, "patient.Telecom[0].System.Value");
        ParserAssert.Equal("555-0100", ParserAssert.NotNull(telecom.Value, "patient.Telecom[0].Value").Value, "patient.Telecom[0].Value.Value");
        ParserAssert.Equal("home", ParserAssert.NotNull(telecom.Use, "patient.Telecom[0].Use").Value, "patient.Telecom[0].Use.Value");

        ParserAssert.Count(1, patient.Address, "patient.Address");
        var address = patient.Address[0];
        ParserAssert.Equal("home", ParserAssert.NotNull(address.Use, "patient.Address[0].Use").Value, "patient.Address[0].Use.Value");
        ParserAssert.Count(1, address.Line, "patient.Address[0].Line");
        ParserAssert.Equal("534 Erewhon St", address.Line[0].Value, "patient.Address[0].Line[0].Value");
        ParserAssert.Equal("PleasantVille", ParserAssert.NotNull(address.City, "patient.Address[0].City").Value, "patient.Address[0].City.Value");
        ParserAssert.Equal("Vic", ParserAssert.NotNull(address.State, "patient.Address[0].State").Value, "patient.Address[0].State.Value");
        ParserAssert.Equal("3999", ParserAssert.NotNull(address.PostalCode, "patient.Address[0].PostalCode").Value, "patient.Address[0].PostalCode.Value");
    }

    private static void AssertPatientWithListName(FhirJsonParser parser, string json)
    {
        var patient = parser.Parse<Patient>(json);

        ParserAssert.Equal("patient-list-name", patient.Id, "patient.Id");
        ParserAssert.Count(1, patient.Name, "patient.Name");

        var name = patient.Name[0];
        ParserAssert.Equal("F", ParserAssert.NotNull(name.Family, "patient.Name[0].Family").Value, "patient.Name[0].Family.Value");
        ParserAssert.Count(1, name.Given, "patient.Name[0].Given");
        ParserAssert.Equal("John", name.Given[0].Value, "patient.Name[0].Given[0].Value");
    }
}
