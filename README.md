📦 Sanitary Pipe Sizer – IPC-Compliant Revit Add-in

A Revit C# API tool for automating the sizing of sanitary drainage pipes per the International Plumbing Code (IPC).

This add-in was developed by Derek Eubanks, PE – a licensed mechanical engineer and Georgia Tech MSCS (Machine Learning) student – to bring code-accurate plumbing design logic directly into the BIM workflow. It is designed for MEP professionals who need reliable, enforceable sanitary pipe sizing during early design and QA.

⸻

🔍 What It Does

When a user selects sanitary pipes in a Revit model and runs the command:
	•	✔️ Reads each pipe’s Drainage Fixture Unit (DFU) value
	•	✔️ Determines if the pipe is a vertical stack or horizontal drain
	•	✔️ Applies IPC-based diameter logic using official tables:
	•	Vertical Stacks → IPC Table 710.1(1)
	•	Horizontal Drains → IPC Table 710.1(2)
	•	Horizontal Branch Limits → IPC Table 703.2
	•	Slope Enforcement → IPC §704.1
	•	No Size Reductions → IPC §710.1.8
	•	✔️ Updates the pipe’s diameter in the Revit model (in inches)
	•	✔️ Displays a summary popup showing how many pipes were sized

⸻

📘 Code Compliance Overview

The tool implements logic directly aligned with IPC:

| IPC Reference         | Description                                                                  | Implemented In                                       |
|-----------------------|------------------------------------------------------------------------------|------------------------------------------------------|
| **Table 710.1(1)**    | Maximum DFU capacities for vertical stacks                                   | `GetVerticalStackDiameter()`                         |
| **Table 710.1(2)**    | Maximum DFU capacities for horizontal building drains and sewers             | `GetHorizontalDrainDiameter()`                       |
| **Table 703.2**       | Maximum allowable DFU on horizontal branches based on pipe diameter          | `ApplyBranchLimits()`                                |
| **§704.1**            | Required minimum slope for horizontal drains based on pipe size              | Checked in `GetHorizontalDrainDiameter()`            |
| **§710.1.8**          | Prohibition against reducing pipe diameter in the direction of flow          | Enforced in `SizeSelectedSanitaryPipes()`            |
| **§710.1**            | General requirement for sizing drainage piping per DFU + slope               | Entire sizing logic foundation                       |
| **§703.1**            | Use of fixture unit values to size DWV piping                                | Foundation for DFU parameter extraction              |
| **§710.1.2 – 710.1.10** | Various provisions on sizing, slope, and system layout (vertical offsets, etc.) | Supported through structured sizing logic       |
✅ Notes
	•	This tool focuses specifically on sanitary drainage pipe sizing. Vent, storm, and building sewer logic are not included (yet).
	•	All values (diameters, slopes) are expressed in imperial units consistent with IPC.

🛠️ Technical Stack
	•	Language: C#
	•	API: Autodesk Revit API (.NET)
	•	Revit Version: 2024+ recommended
	•	Platform: Desktop Add-in (.addin file required)
	•	No external dependencies

⸻

🚀 How to Use
	1.	Open your Revit model and select sanitary pipes.
	2.	Run the add-in from: Add-Ins → External Tools → Sanitary Pipe Sizer

 	3.	Pipes will be resized in-place using IPC-compliant logic.
	4.	A popup will confirm how many pipes were updated.

⸻

🔄 Future Enhancements
	•	✅ Interpolation for DFU-to-GPM + velocity tagging (flush valve logic)
	•	✅ Integration with Revit shared parameters for documentation
	•	✅ CSV report output for QA/QC
	•	✅ UPC or metric code logic (toggleable)

⸻

👤 About the Developer

Derek Eubanks, PE
Licensed Mechanical Engineer | GT OMSCS – Machine Learning
Revit C# API Developer | HVAC & Plumbing Systems | BIM Workflow Automator

This tool is part of my larger effort to bring engineering-grade logic into the BIM environment using real code. I build tools that bridge mechanical design, code compliance, and intelligent automation using C# and Python.
