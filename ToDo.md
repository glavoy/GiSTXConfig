## To Do

- [ ] add database file name back to from config.json - for minor changes, we can use the same database - i.e. adding a new field.
- [ ] update ro remove the reference to '(see The CRFS Worksheet)' in the ReadMe.md
- [ ] Add 'time' type
- [ ] Add 'button' question type?
- [ ] check this: `date` must have `fieldtype` = `date` or `datetime`
- [ ] revisit field type `text_id` - is it needed?
- [ ] phone_num, text_id, hourmin - revisit how these work
- [ ] revisit verification of field names, etc for dynamic responses
	- Referenced field names must exist in the same worksheet
	- Referenced fields must appear before the current question
- [ ] re-examine how skips are preformed - add compund logic for skips - update README - it is currently incorrect
- [ ] check `auto_start_repeat` and `repeat_enforce_count` in README
- [ ] add more checks for 'skips' - cannot allow a preskip on the samek question. i.e. cannot use the very filedname for the question in the preskip
- [ ] 