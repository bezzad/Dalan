// CLI-64: which guide steps are done, from what the server says. Pure, so tests run without a browser.
import { builderView, type BuilderState } from "./panels.ts";

export type StepState = "done" | "current" | "todo";

interface Who { builder: BuilderState; admin: boolean; has_phone: boolean }

/** CLI-64: a done step stays done; the first step not done is the current one; the rest wait. */
export function stepStates(done: boolean[]): StepState[] {
  const current = done.indexOf(false);
  return done.map((isDone, i) => isDone ? "done" : i === current ? "current" : "todo");
}

/**
 * CLI-64: the site guide (log in as a builder, install, key, try it, keep it online).
 * An online site proves the agent is installed, logged in and tried. "Keep it online" the server cannot know.
 */
export function siteDone(facts: { user: Who; keys: number; sitesOnline: number }): boolean[] {
  const online = facts.sitesOnline > 0;
  return [builderView(facts.user) === "bots", online, facts.keys > 0 || online, online, false];
}

/** CLI-64: the bot guide (log in, phone, builder, a bot made); the steps after the first bot the server cannot know. */
export function botDone(facts: { user: Who; bots: number }): boolean[] {
  const { user } = facts;
  const builder = builderView(user) === "bots";
  return [true, user.has_phone || builder, builder, facts.bots > 0];
}
