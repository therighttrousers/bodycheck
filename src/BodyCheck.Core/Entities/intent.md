# Entities

## What it is

The things in a room that have a body, as the engine's own rigid bodies: their shape and mass, their contacts, and the motors each kind of body moves itself by. It also says what a controller of a body is, without any particular one.

## How it relates to the rest

It depends only on Core's root. Whatever decides what an entity does, and whatever puts entities in a room, builds on this subsystem; nothing here knows about them.

## What it is not for

Behaviour: what the player's input or an NPC makes a body do. Where entities start, or which ones a room has. Saving and loading.
