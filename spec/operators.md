# Operators (draft v0)

The generic building blocks a composed stage is made of. Each is product-agnostic: everything specific to a product
is a named **judgment** (see [manifest.md](manifest.md#stage)) handed to the operator. An operator is defined by the
rules below and, authoritatively, by its examples in the [conformance suite](conformance/): every implementation
must pass them.

The operators chain: `group-by` turns a list of items into **groups** (an ordered list of non-empty, ordered lists
of items); `order-by` and `session` take groups and return groups; `split-small-groups` takes groups and returns
**labelled groups**. No operator changes, drops or duplicates an item: every item of the input appears exactly once in
the output.

## Values

Judgments return plain values, which the operators compare:

- **Equal** (group-by): two values are equal when they are the same kind and the same value: text by its exact
  characters (case-sensitive), numbers by value, booleans by value. Text and a number are never equal, even when
  they look alike (`"1"` and `1`). `null` equals `null`.
- **Ordered** (order-by): text in Unicode code point order (ordinal, not culture-aware: `"B" < "_" < "a" < "é"`, and
  `"10" < "9"`); numbers by value; `false` before `true`; times chronologically. All the non-null values one judgment
  returns are of one kind.
- **Absent**: a judgment may have no value for an item (`null`). How each operator treats it is stated below.

Times (session's `at`, and times compared by order-by) are local date-times with no offset, compared as instants on
one clock.

## group-by

Splits a list of items into groups that share a key.

| Parameter | Meaning |
|---|---|
| `key` | Judgment: the item's key. |

- One group per distinct key, in the order each key **first appears** in the input.
- Within a group, items keep their input order.
- Items with no key (`null`) form one group together, placed where the first of them appears.
- Empty input gives no groups. Groups are never empty.

## order-by

Sorts the items within each group.

| Parameter | Meaning |
|---|---|
| `by` | One or more judgments, compared in turn: the second only breaks ties of the first, and so on. |

- Groups stay in place; only the items inside each group move.
- The sort is **stable**: items equal on every key keep their input order.
- An item with no value for a key sorts **after** every item with one; items with no value tie with each other (so the
  next key, or input order, decides).
- Empty input gives no groups.

The C# reference takes one or two keys. It compares text by UTF-16 code unit, which agrees with code point order except
between characters above U+FFFF and those from U+E000 to U+FFFF; no case depends on that difference.

## session

Splits each ordered group into sessions: runs of items close together in time.

| Parameter | Meaning |
|---|---|
| `at` | Judgment: the item's time, or none. |
| `maxGap` | A duration, zero or more. |
| `breakWhen` | Zero or more judgments, each asked about (the session so far, the next item). |

Walking each group in order, an item **joins** the current session when all of these hold:

1. the current session is not empty (the first item of a group always starts a session, and no judgment is asked
   about it);
2. the item has a time, and so does the previous item;
3. the item's time minus the previous item's time is **at most** `maxGap` (a gap exactly equal to `maxGap` joins);
4. no `breakWhen` judgment holds.

Otherwise it starts a new session. Consequences, each pinned by a case:

- An item with no time is a session of its own, and the item after it starts a new session.
- The gap is measured from the **previous** item, not the first of the session, so a session can be longer than
  `maxGap`.
- Sessions never span groups.
- Session does not sort. An item earlier than the previous one has a negative difference, which is at most `maxGap`,
  so it joins. Put an `order-by` on the time first when that is not wanted.
- A `maxGap` of zero joins only items at the same time.
- Empty input gives no groups.

## split-small-groups

Labels each group by size, splitting small ones into single items.

| Parameter | Meaning |
|---|---|
| `minimum` | A whole number, 1 or more. |
| `keptAs` | The label of a group kept whole. |
| `splitAs` | The label of each group of one made from a smaller group. |

- A group with **at least** `minimum` items is kept whole, labelled `keptAs`.
- A smaller group becomes one group per item, each labelled `splitAs`, in the item order, in the place of the group
  it came from.
- The label goes by the size of the input group, so with `minimum` 1 every group, even a group of one, is `keptAs`.
- Empty input gives no groups.

On the wire a labelled group is `{ "label": ..., "items": [ ... ] }`.

## In the manifest

A step lists its parameters as text written for a reviewer, not as the typed data of the conformance suite: judgments
by name, `maxGap` with a unit (`"90 s"`, `"5 min"`), several `by` keys joined by `", then "`, several `breakWhen`
judgments by `" or "` (see [manifest.md](manifest.md#stage)). Each operator's `phrase` is listed in the
[C# reference](../src/Seamlineage.Operators/).
