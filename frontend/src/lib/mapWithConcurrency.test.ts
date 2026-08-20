import { expect, test } from "bun:test";
import { mapWithConcurrency } from "./mapWithConcurrency";

test("mapWithConcurrency preserves order and caps concurrent work", async () => {
  let active = 0;
  let peak = 0;

  const result = await mapWithConcurrency([1, 2, 3, 4, 5, 6], 2, async (value) => {
    active += 1;
    peak = Math.max(peak, active);
    await Bun.sleep((7 - value) * 2);
    active -= 1;
    return value * 10;
  });

  expect(result).toEqual([10, 20, 30, 40, 50, 60]);
  expect(peak).toBe(2);
});

test("mapWithConcurrency rejects invalid concurrency limits", async () => {
  await expect(mapWithConcurrency([1], 0, async (value) => value)).rejects.toThrow(
    "Concurrency limit must be a positive integer"
  );
});
