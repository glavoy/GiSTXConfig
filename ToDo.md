## To Do

- [ ] add database file name back to from config.json - for minor changes, we can use the same database - i.e. adding a new field.
- [ ] add 'pathToCSVfiles' in config.json - as an optional parameter
```
{
  "excelFile": "C:\\GeoffOffline\\GiSTConfigX\\Excel\\prismcss.xlsx",
  "outputPath": "C:\\temp\\",
  "surveyName": "PRISM CSS 2025-12-01",
  "surveyId": "prism_css_2025_12_01"
}
```

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
- [ ] create parser for 'automatic' variables