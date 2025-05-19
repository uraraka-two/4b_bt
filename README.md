# Knuth 4B exercise

exercise backtrace for 7.2.2.1 398.

## Running tests

The `dlx.sln` solution includes a regression test project. Execute all tests
with:

```bash
dotnet test pentomino_programs/dlx.Tests
```

## Running the solver

The solver accepts piece flags individually or combined. For example, the
following commands are equivalent:

```bash
dotnet run -- -l -y -v -t -w -z
dotnet run -- -lyvtwz
```
