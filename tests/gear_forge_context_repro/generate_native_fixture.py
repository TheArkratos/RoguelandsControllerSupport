#!/usr/bin/env python3
"""Generate a local test input from the user's reference; never bundle game code."""

import argparse
import os
from pathlib import Path
import tempfile


SIGNATURE = "public int GetRecipeUnlocked0(int p, int n)"
RECORD_CALL = "UnlockCalls++; LastUnlockPage = p; LastUnlockCell = n;"


def extract_method(source):
    if source.count(SIGNATURE) != 1:
        raise ValueError("Expected exactly one native GetRecipeUnlocked0 signature.")
    start = source.index(SIGNATURE)
    opening = source.find("{", start)
    if opening < 0:
        raise ValueError("Native getter has no method body.")
    # This audited getter contains no strings or comments. Refuse changed syntax
    # rather than incorrectly counting braces embedded in non-code tokens.
    depth = 0
    for index in range(opening, len(source)):
        token = source[index]
        if token in ('"', "'") or source[index:index + 2] in ("//", "/*"):
            raise ValueError("Native getter syntax changed; review extraction before regenerating.")
        if token == "{":
            depth += 1
        elif token == "}":
            depth -= 1
            if depth == 0:
                method = source[start:index + 1]
                return method.replace("{", "{\n        " + RECORD_CALL, 1)
    raise ValueError("Native getter body has unbalanced braces.")


def generate(reference_dir, output):
    source = reference_dir / "Assembly-CSharp" / "GameScript.cs"
    if not source.is_file():
        raise ValueError("Missing local GameScript.cs. Set ROGUELANDS_REFERENCE_DIR "
                         "to your GameReference/Generated directory.")
    method = extract_method(source.read_text())
    # All scaffolding below is fixture state, independent of the extracted method.
    generated = """// Local generated research input; do not commit or redistribute.
public partial class GameScript
{
    private int[,] recipeUnlocked0 = new int[10, 12];
    public int UnlockCalls, LastUnlockPage, LastUnlockCell, PreviousPageCalls, NextPageCalls;
    public void UnlockGear(int row, int cell) { recipeUnlocked0[row, cell] = 1; }
    public void LockGear(int row, int cell) { recipeUnlocked0[row, cell] = 0; }
    public void RecipeUp() { NextPageCalls++; curRecipePage = (curRecipePage + 1) % (craftType == 0 ? 6 : 2); }
    public void RecipeDown() { PreviousPageCalls++; curRecipePage = (curRecipePage + (craftType == 0 ? 5 : 1)) % (craftType == 0 ? 6 : 2); }
    public int CurrentPage { get { return curRecipePage; } }
""" + method + "\n}\n"
    output.parent.mkdir(parents=True, exist_ok=True)
    if output.is_file() and output.read_text() == generated:
        return
    # The gear fixture and native IL audit can run concurrently.
    with tempfile.NamedTemporaryFile(mode="w", dir=output.parent, delete=False) as temp:
        temp.write(generated)
        temporary = temp.name
    os.replace(temporary, output)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference-dir", type=Path,
                        default=Path(os.environ.get("ROGUELANDS_REFERENCE_DIR") or
                                     Path(__file__).resolve().parents[4] / "GameReference/Generated"))
    parser.add_argument("--output", type=Path,
                        default=Path(__file__).resolve().parent / "obj/NativeGearRecipe.g.cs")
    args = parser.parse_args()
    try:
        generate(args.reference_dir, args.output)
    except (OSError, ValueError) as error:
        parser.exit(1, "Native fixture generation failed: " + str(error) + "\n")


if __name__ == "__main__":
    main()
