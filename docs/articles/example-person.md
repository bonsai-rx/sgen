First, define the JSON Schema for our `Person` data type:

[person.json](~/workflows/person.json)

```json
{
  "title": "Person",
  "type": "object",
  "properties": {
    "Age": { "type": "integer" },
    "FirstName": { "type": "string" },
    "LastName": { "type": "string" },
    "DOB": { "type": "string", "format": "date-time" }
  }
}
```

Generate custom Bonsai extension code using `Bonsai.Sgen`:

```powershell
dotnet bonsai.sgen --schema person.json --output Extensions/PersonSgen.Generated.cs
```

Use the generated operators directly in your Bonsai workflow:

:::workflow
![Person as BonsaiSgen](~/workflows/person-example-bonsai-sgen.bonsai)
:::