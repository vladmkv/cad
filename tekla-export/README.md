# Tekla Open API and AutoCAD interoperability
Code sample by Vladimir Malkov
April 4, 2016

Goal: to provide associative dimensioning for DWG geometry exported from Tekla Structures, and to provide an ability to update exported geometry without losing existing annotations in AutoCAD drawing.

Tekla Structures supports feature-rich export to DWG/DXF, but the simplest entity exported is a 3d Polyline. AutoCAD supports polylines associativity, but not for externally-referenced drawings. This means that user can not maintain his dimensions associative in master drawings after re-exporting to DWG from Tekla Structures.

However, AutoCAD fully supports associativity to simple Lines inside an externally referenced drawing (xref). Therefore, provided that lines are exported in the same order, bearing the same DWG handles, AutoCAD will maintain the associativity to them after xref update.

I’ve implemented a .NET plugin for AutoCAD using the Tekla OpenAPI, which allows to create, insert and update such xrefs. It’s just a prototype to show the concept.

I've created a short screencast video demonstrating the result:
https://knowledge.autodesk.com/community/screencast/30236111-a286-41a5-a1ff-18926cb73c56

## The workflow is:
- Plugin adds a TEKLAPICK command to AutoCAD, prompting user to pick a beam in Tekla Structures
- Once picked, the beam is converted into DWG lines and the whole set is stored as separate drawing named after the part ID of the beam.
- This DWG file is inserted into the current drawing as an xref.
- User can move, annotate, create paper space viewports of the part drawing.
- User can change the part in Tekla Structures: profile, size etc.
- User can run TEKLAPICK command again to update the drawing of the part. This step basically replaces the xref DWG with a new one. The important part happens here. Since drawing is re-created, and part edges are iterated in the same order, their DWG handles correspond to each other before and after update. This enables AutoCAD dimensions to recalculate according to the new geometry.
- It is possible to develop the same export functionality directly in Tekla Structures, making a useful improvement for users preferring to annotate and update part drawings in a DWG editor.