import { strict as assert } from "node:assert";
import { describe, it } from "node:test";

import { parseUseDoppler } from "./config.mjs";

describe("parseUseDoppler", () => {
  it("uses Doppler when USE_DOPPLER is unset or empty", () => {
    assert.equal(parseUseDoppler(undefined), true);
    assert.equal(parseUseDoppler(""), true);
  });

  for (const value of ["false", "FALSE", "0", " false "]) {
    it(`turns Doppler off for '${value}'`, () => {
      assert.equal(parseUseDoppler(value), false);
    });
  }

  for (const value of ["true", "maybe"]) {
    it(`rejects '${value}' and names the accepted value`, () => {
      assert.throws(
        () => parseUseDoppler(value),
        (error: Error) =>
          /USE_DOPPLER/.test(error.message) &&
          error.message.includes(`'${value}'`) &&
          /false/.test(error.message),
      );
    });
  }
});
