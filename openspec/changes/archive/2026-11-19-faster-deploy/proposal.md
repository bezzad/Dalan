# Proposal

## Why

The owner (2026-11-19) said a merge took about 17 minutes to reach the server. Tests took 9 to 12 minutes on one machine, the deploy job built the release a second time, and the whole release was uploaded each time.

## What Changes

- CI builds once (`build` job) and keeps the release and the built tests as files of the run.
- Each test project runs on its own machine, all at the same time. Specs, rule, deploy and proxy checks and the Docker image run next to the build.
- The deploy uses the same release the tests passed with, and sends it with rsync: only the parts that differ from the current release go over the network.
- The server needs `rsync` (setup installs it).

## Capabilities

- delivery: DEL-04 says only the changes are sent.
