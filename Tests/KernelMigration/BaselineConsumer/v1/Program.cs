using MyFhirSdk.Core;
using MyFhirSdk.Primitives;
using MyFhirSdk.Resources;
using MyFhirSdk.Serialization.Json;

PrimitiveType<bool?> primitive = new FhirBoolean { Value = true };
if (primitive.Value != true) throw new Exception("Primitive read failed.");
primitive.Value = false;
if (primitive.Value != false) throw new Exception("Primitive write failed.");
var parser = new FhirJsonParser();
var serializer = new FhirJsonSerializer();
var patient = parser.Parse<Patient>("""{"resourceType":"Patient","id":"k0-consumer","active":true}""");
Base model = patient;
if (model is not Resource || patient.Active?.Value != true)
    throw new Exception("Model/base assignment failed.");
var roundTrip = parser.Parse<Patient>(serializer.Serialize(patient));
if (roundTrip.Id != "k0-consumer" || roundTrip.Active?.Value != true)
    throw new Exception("Public parser/serializer round-trip failed.");
Console.WriteLine("Consumer passed: primitive access, model/base assignment, JSON round-trip.");
