using System.Text.Json.Nodes;
using Seamlineage.Docs;

namespace Seamlineage.Tests;

/// <summary>An example case as a compact table of input → outcome, read through the stage's declared view or a generic one.</summary>
public class ExampleTableTests
{
    private static JsonNode View(string json) => JsonNode.Parse(json)!;

    private const string BasketView = """
        {
          "rows": "accepted",
          "columns": [ { "label": "pick", "path": "id" }, { "label": "picked at", "path": "pickedAt" } ],
          "outcome": { "from": "groups", "members": "picks", "match": "id", "columns": [ { "label": "basket", "path": "key" }, { "label": "kind", "path": "kind" } ] }
        }
        """;

    private const string Input = """
        { "accepted": [
            { "id": "p1", "pickedAt": "2026-09-12T09:00:00" },
            { "id": "p2", "pickedAt": null },
            { "id": "p3", "pickedAt": "2026-09-12T09:01:00" }
        ], "rejected": [] }
        """;

    private const string Expected = """
        { "groups": [
            { "key": "p1", "kind": "basket", "picks": [ { "id": "p1" }, { "id": "p3" } ] },
            { "key": "p2", "kind": "loose", "picks": [ { "id": "p2" } ] }
        ] }
        """;

    [Fact]
    public void Shows_one_row_per_input_item_with_the_outcome_of_the_output_group_it_ended_up_in()
    {
        var table = ExampleTable.Render(View(BasketView), Input, Expected);

        Assert.Equal(
            """
            | pick | picked at | → basket | → kind |
            |---|---|---|---|
            | p1 | 2026-09-12T09:00:00 | p1 | basket |
            | p2 | null | p2 | loose |
            | p3 | 2026-09-12T09:01:00 | p1 | basket |

            """.ReplaceLineEndings("\n"),
            table);
    }

    [Fact]
    public void A_row_no_output_group_holds_shows_a_dash_for_its_outcome()
    {
        var table = ExampleTable.Render(View(BasketView), Input, """{ "groups": [ { "key": "p1", "kind": "basket", "picks": [ { "id": "p1" } ] } ] }""");

        Assert.Contains("| p2 | null | – | – |", table);
    }

    [Fact]
    public void Without_members_an_output_record_itself_is_matched()
    {
        var view = """
            { "rows": "picks", "columns": [ { "label": "pick", "path": "id" } ],
              "outcome": { "from": "rejected", "match": "id", "columns": [ { "label": "rejected because", "path": "reason" } ] } }
            """;

        var table = ExampleTable.Render(View(view), """{ "picks": [ { "id": "p1" }, { "id": "p2" } ] }""", """{ "rejected": [ { "id": "p2", "reason": "noWeight" } ] }""");

        Assert.Contains("| p1 | – |", table);
        Assert.Contains("| p2 | noWeight |", table);
    }

    [Fact]
    public void A_view_without_an_outcome_shows_the_input_columns_only()
    {
        var table = ExampleTable.Render(View("""{ "rows": "picks", "columns": [ { "label": "pick", "path": "id" } ] }"""), """{ "picks": [ { "id": "p1" } ] }""", "{}");

        Assert.Equal("| pick |\n|---|\n| p1 |\n", table);
    }

    [Fact]
    public void Paths_step_through_fields_and_list_positions_and_an_empty_path_is_the_whole_document()
    {
        var view = """{ "rows": "", "columns": [ { "label": "first tag", "path": "tags.0" }, { "label": "city", "path": "address.city" }, { "label": "missing", "path": "nope.deeper" } ] }""";

        var table = ExampleTable.Render(View(view), """[ { "tags": [ "a", "b" ], "address": { "city": "Leeds" } } ]""", "{}");

        Assert.Contains("| a | Leeds |  |", table);
    }

    [Fact]
    public void Summarises_nested_values_and_escapes_pipes_and_line_breaks()
    {
        var view = """{ "rows": "rows", "columns": [ { "label": "list", "path": "list" }, { "label": "one", "path": "one" }, { "label": "none", "path": "none" }, { "label": "record", "path": "record" }, { "label": "text", "path": "text" }, { "label": "n", "path": "n" }, { "label": "b", "path": "b" } ] }""";

        var table = ExampleTable.Render(View(view), """{ "rows": [ { "list": [1, 2, 3], "one": [1], "none": [], "record": { "a": 1 }, "text": "a | b\nc", "n": 1.5, "b": true } ] }""", "{}");

        Assert.Contains("| 3 items | 1 item | 0 items | {…} | a \\| b c | 1.5 | true |", table);
    }

    [Fact]
    public void Reports_a_rows_path_that_is_not_a_list_and_says_so_on_the_page()
    {
        var problems = new List<string>();

        var table = ExampleTable.Render(View("""{ "rows": "accepted", "columns": [] }"""), """{ "picks": [] }""", "{}", problems);

        Assert.Contains("The example view's rows path `accepted` is not a list in this case's input.json.", table);
        Assert.Equal(["the example view's rows path 'accepted' is not a list in input.json"], problems);
    }

    [Fact]
    public void Reports_an_outcome_path_that_is_not_a_list()
    {
        var problems = new List<string>();

        ExampleTable.Render(View(BasketView), Input, """{ "baskets": [] }""", problems);

        Assert.Equal(["the example view's outcome path 'groups' is not a list in expected.json"], problems);
    }

    [Fact]
    public void Without_a_view_shows_the_top_level_lists_of_the_input_and_the_expected_output()
    {
        var table = ExampleTable.Render(
            null,
            """{ "picks": [ { "id": "p1", "weightGrams": 0 }, { "id": "p2", "variety": "gala", "tags": ["x"] } ] }""",
            """{ "accepted": [], "total": 170, "note": null }""");

        Assert.Equal(
            """
            Input `picks`:

            | id | weightGrams | variety | tags |
            |---|---|---|---|
            | p1 | 0 |  |  |
            | p2 |  | gala | 1 item |

            Expected `accepted`: none

            Expected `total`: 170

            Expected `note`: null

            """.ReplaceLineEndings("\n"),
            table);
    }

    [Fact]
    public void Without_a_view_a_list_of_plain_values_is_written_inline_and_a_top_level_list_is_one_table()
    {
        var table = ExampleTable.Render(null, """[ { "id": "a" } ]""", """{ "ids": [ "a", "b" ] }""");

        Assert.Equal("Input:\n\n| id |\n|---|\n| a |\n\nExpected `ids`: a, b\n", table);
    }

    [Fact]
    public void Says_when_a_case_has_no_input_or_expected_file()
    {
        Assert.Equal("Input: no input.json.\n\nExpected: no expected.json.\n", ExampleTable.Render(null, null, null));
    }
}
