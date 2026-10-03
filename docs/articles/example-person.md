First, define the JSON Schema for the `Person` type:

[person.json](~/workflows/person.json)

```json
{
  "title": "Person",
  "type": "object",
  "properties": {
    "age": { "type": "integer" },
    "first_name": { "type": "string" },
    "last_name": { "type": "string" },
    "date_of_birth": { "type": "string", "format": "date-time" }
  }
}
```

Then generate the extension code with `Bonsai.Sgen`:

```powershell
dotnet bonsai.sgen person.json -o Extensions --serializer json
```

The generated code follows the naming conventions of C#, so the `first_name` property in the schema becomes `FirstName` in the generated `Person` type. Serialized data keeps the names declared in the schema.

Use the generated operators directly in a workflow:

:::workflow
![Person as BonsaiSgen](~/workflows/person-example-bonsai-sgen.bonsai)
:::