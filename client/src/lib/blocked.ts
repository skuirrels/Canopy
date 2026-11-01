// The first rule that applies explains why an action is unavailable; undefined means it can proceed.
export function blockedBy(...rules: ReadonlyArray<readonly [unknown, string]>): string | undefined {
 return rules.find(([blocked]) => blocked)?.[1];
}
