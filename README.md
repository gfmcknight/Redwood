# Redwood
Redwood is a toy programming language interpreted in C#.

Redwood is being made to experiment with the concept of persisted closures,
which is where the program state from a closured lambda used as a callback can
be serialized and then rehydrated at a later time, potentially in a different
context or on a different machine.

However, the development of this language is still in early development and it
lacks the groundwork necessary to implement these features at the moment.

## Design
Currently, compilation is done in 3 distinct passes.

The first is the walking pass, where declared variables are created, and all used
identifiers are collected and the two (variables and their uses) are matched to
each other. By the end of this phase, it is known whether a variable is ever
modified/mutated and whether a variable needs to be closured.

The second is the binding pass, where declared variables are given a direct location,
either on the stack or in a closure. By the end of this phase, types of expressions
are known where possible (since all names have been matched, particularly the names
that come from imports), as are the amount of space that needs to be allocated on the
stack and in the closure for every variable.

The third is the actual compilation phase in which the variable and known type
information is used to determine the set of instructions to generate.