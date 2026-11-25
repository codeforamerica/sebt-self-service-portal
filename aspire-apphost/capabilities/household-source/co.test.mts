import { strict as assert } from "node:assert";
import { describe, it } from "node:test";

import type { ConfigSetting } from "../requirements.mjs";
import { coHouseholdMessage, coHouseholdSettings } from "./co.mjs";

const phoneOverrideKey = "DevelopmentPhoneOverride__Phone";

function settingFor(settings: ConfigSetting[], key: string): ConfigSetting | undefined {
  return settings.find((setting) => setting.key === key);
}

describe("coHouseholdSettings without appsettings.co.json", () => {
  const settings = coHouseholdSettings(false);

  it("turns on both mock flags", () => {
    assert.equal(settingFor(settings, "FeatureManagement__use_mock_household_data")?.value, "true");
    assert.equal(settingFor(settings, "FeatureManagement__use_mock_cbms_responses")?.value, "true");
  });

  it("clears the development phone override", () => {
    const override = settingFor(settings, phoneOverrideKey);
    assert.equal(override?.value, "");
    assert.equal(override?.kind, "neutralize");
  });
});

describe("coHouseholdSettings with appsettings.co.json", () => {
  const settings = coHouseholdSettings(true);

  it("leaves the mock flags to the file", () => {
    assert.ok(!settings.some((setting) => setting.key.startsWith("FeatureManagement__")));
  });

  it("leaves the development phone override to the file", () => {
    assert.equal(settingFor(settings, phoneOverrideKey), undefined);
  });
});

for (const hasLocalSettings of [false, true]) {
  describe(`coHouseholdSettings when hasLocalSettings is ${hasLocalSettings}`, () => {
    const settings = coHouseholdSettings(hasLocalSettings);

    it("seeds Colorado households and looks them up by phone", () => {
      assert.equal(settingFor(settings, "Seeding__State")?.value, "co");
      assert.equal(settingFor(settings, "StateHouseholdId__PreferredHouseholdIdTypes__0")?.value, "Phone");
    });

    it("says why each setting is there", () => {
      for (const setting of settings) {
        assert.ok(setting.why.trim().length > 0, `${setting.key} has an empty why`);
      }
    });
  });

  describe(`coHouseholdMessage when hasLocalSettings is ${hasLocalSettings}`, () => {
    const message = coHouseholdMessage(hasLocalSettings);

    it("names the file and the settings that choose the household data", () => {
      for (const expected of [
        "appsettings.co.json",
        "use_mock_household_data",
        "use_mock_cbms_responses",
        "DevelopmentPhoneOverride",
      ]) {
        assert.ok(message.includes(expected), `message does not mention ${expected}`);
      }
    });
  });
}

describe("coHouseholdMessage without appsettings.co.json", () => {
  it("names the example file to copy", () => {
    assert.ok(coHouseholdMessage(false).includes("appsettings.co.example.json"));
  });
});
