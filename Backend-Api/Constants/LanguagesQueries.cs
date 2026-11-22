namespace Backend_Api.Constants;

public static class LanguagesQueries
{
    public static readonly Dictionary<string, LangQuery> Queries = new Dictionary<string, LangQuery>
{
    // class Person { private int id; }
    ["JAVA"] = new("""
        (class_declaration
          name: (identifier) @class
          body: (class_body
            (field_declaration
              type: (_) @type
              declarator: (variable_declarator name: (identifier) @name))))
        """, new[] { "class", "type", "name" }),

    // class Person { public int Id { get; set; } }
    ["C#"] = new("""
        (class_declaration
          name: (identifier) @class
          body: (declaration_list
            (property_declaration type: (_) @type name: (identifier) @name)))
        """, new[] { "class", "type", "name" }),

    // @dataclass class Person:  id: int
    ["PYTHON"] = new("""
        (class_definition
          name: (identifier) @class
          body: (block
            (assignment
              left: (identifier) @name
              type: (type) @type)))
        """, new[] { "class", "name", "type" }),

    // class/struct Person { int id; }
    ["C++"] = new("""
        [
          (class_specifier
            name: (type_identifier) @class
            body: (field_declaration_list
              (field_declaration type: (_) @type declarator: (field_identifier) @name)))
          (struct_specifier
            name: (type_identifier) @class
            body: (field_declaration_list
              (field_declaration type: (_) @type declarator: (field_identifier) @name)))
        ]
        """, new[] { "class", "type", "name" }),

    // class Person { id = 0; }   (no types in JavaScript)
    ["JAVASCRIPT"] = new("""
        (class_declaration
          name: (identifier) @class
          body: (class_body
            (field_definition property: (property_identifier) @name)))
        """, new[] { "class", "name" }),

    // type Person struct { Id int }
    ["GO"] = new("""
        (type_declaration
          (type_spec
            name: (type_identifier) @class
            type: (struct_type
              (field_declaration_list
                (field_declaration
                  name: (field_identifier) @name
                  type: (_) @type)))))
        """, new[] { "class", "name", "type" }),
    ["DART"] = new("""
    (class_definition
      name: (identifier) @class
      body: (class_body
        (declaration
          (initialized_identifier_list
            (initialized_identifier (identifier) @name))) @decl))
    """, new[] { "class", "decl", "name" }),
    ["TYPESCRIPT"] = new("""
    [
      (interface_declaration
        name: (type_identifier) @class
        (_ (property_signature
             name: (property_identifier) @name
             type: (type_annotation (_) @type))))

      (class_declaration
        name: (type_identifier) @class
        body: (class_body
          (public_field_definition
            name: (property_identifier) @name
            type: (type_annotation (_) @type))))

      (type_alias_declaration
        name: (type_identifier) @class
        value: (object_type
          (property_signature
            name: (property_identifier) @name
            type: (type_annotation (_) @type))))
    ]
    """, new[] { "class", "name", "type" }),

};

public record LangQuery(string Query, string[] Roles);
}
