using System;
using System.IO;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using AcDb = Autodesk.AutoCAD.DatabaseServices;
using AcGe = Autodesk.AutoCAD.Geometry;

using Tekla.Structures.Model;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model.UI;
using Tekla.Structures.Solid;

namespace TeklaSamples
{
    public class XrefExport
    {
        [CommandMethod("TeklaPick")]
        public void TeklaPickCommand()
        {
            var masterDb = Editor().Document.Database;
            var masterDir = GetDbDirectory(masterDb);

            if (0 == masterDir.Length)
            {
                Editor().WriteMessage("Please save the master drawing first!");
                return;
            }

            Part part = PickTeklaBeam();

            if (part != null)
            {
                UpdatePartDrawing(part);
            }
        }

        static private void UpdatePartDrawing(Part part)
        {
            AcDb.Database xrefDb = BuildXrefDatabase(part.GetSolid());

            if (xrefDb != null)
            {
                var masterDb = Editor().Document.Database;
                var masterDir = GetDbDirectory(masterDb);

                var xrefName = "TEK_" + part.Identifier.ToString();
                var fileName = xrefName + ".dwg";
                var fullPath = masterDir + @"\" + fileName;

                // Replace existing XRef with a new one.
                File.Delete(fullPath);

                xrefDb.SaveAs(fullPath, AcDb.DwgVersion.Current);

                // Attach and insert xref only for the first time. 
                // User can add more block references later. 
                if (!XrefExists(masterDb, xrefName))
                {
                    XrefAttachAndInsert(masterDb, fullPath, AcGe.Point3d.Origin);
                }

                // Refresh the master drawing.
                var doc = Application.DocumentManager.MdiActiveDocument;

                // Update the reference.
                doc.SendStringToExecute("_-XREF _Reload " + xrefName + "\n", false, false, true);

                // Update dimensions.
                doc.SendStringToExecute("_DIMREGEN ", false, false, true);
            }
        }

        static private Editor Editor()
        {
            return Application.DocumentManager.MdiActiveDocument.Editor;
        }

        static public Part PickTeklaBeam()
        {
            Editor().WriteMessage("Pick a beam in Tekla...\n");

            Model model = new Model();

            if (model.GetConnectionStatus())
            {
                Picker picker = new Picker();
 
                try
                {
                    ModelObject modelObj = picker.PickObject(
                        Picker.PickObjectEnum.PICK_ONE_OBJECT, "Pick a beam");

                    Beam beam = modelObj as Beam;

                    if (beam != null)
                    {
                        Editor().WriteMessage("Part ID: " + beam.Identifier.ToString() 
                            + "\n");

                        return beam as Part;
                    }
                    else
                    {
                        Editor().WriteMessage("Picked object is not a Beam\n");
                    }
                }
                catch (System.Exception ex)
                {
                    Editor().WriteMessage("Error: " + ex.ToString());
                }
            }
            else
            {
                Editor().WriteMessage("Please start Tekla Structures first!\n");
            }

            return null;
        }

        static private AcDb.Database BuildXrefDatabase(Solid partSolid)
        {
            var db = new AcDb.Database();

            using (var trans = db.TransactionManager.StartTransaction())
            {
                var blockTable = trans.GetObject(db.BlockTableId, AcDb.OpenMode.ForRead) 
                    as AcDb.BlockTable;

                var modelSpaceBlock = trans.GetObject(blockTable[AcDb.BlockTableRecord.ModelSpace],
                    AcDb.OpenMode.ForWrite) as AcDb.BlockTableRecord;

                try
                {
                    int count = CollectEdgesToBlock(partSolid, modelSpaceBlock, trans);

                    trans.Commit();

                    Editor().WriteMessage(count + " edges exported");
                }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    Editor().WriteMessage("Error: " + ex.Message);

                    return null;
                }
            }

            return db;
        }

        static private bool XrefExists(AcDb.Database db, string xrefName)
        {
            bool exists = false;
            
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var blockTable = (AcDb.BlockTable)tr.GetObject(db.BlockTableId, AcDb.OpenMode.ForRead);

                exists = blockTable.Has(xrefName);

                tr.Commit();
            }

            return exists;
        }

        public static bool XrefAttachAndInsert(AcDb.Database db, string xrefPath, AcGe.Point3d pos)
        {
            if (!File.Exists(xrefPath))
                return false;

            var xrefName = Path.GetFileNameWithoutExtension(xrefPath);

            try
            {
                using (var trans = db.TransactionManager.StartOpenCloseTransaction())
                {
                    var xId = db.AttachXref(xrefPath, xrefName);

                    if (xId.IsValid)
                    {
                        var blockTable = trans.GetObject(db.BlockTableId, AcDb.OpenMode.ForRead) as AcDb.BlockTable;

                        var btRecord = trans.GetObject(blockTable[AcDb.BlockTableRecord.ModelSpace],
                            AcDb.OpenMode.ForWrite) as AcDb.BlockTableRecord;

                        var blockRef = new AcDb.BlockReference(pos, xId);

                        btRecord.AppendEntity(blockRef);
                        trans.AddNewlyCreatedDBObject(blockRef, true);
                    }

                    trans.Commit();
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
                ed.WriteMessage("Attach error: "+ ex.ToString() + "\n");

                return false;
            }

            return true;
        }

        public static string GetDbDirectory(AcDb.Database dbMaster)
        {
            var fullPath = dbMaster.Filename;
            var fileName = Path.GetFileName(fullPath);

            // .dwt is template file name of unsaved drawing.
            if (!fileName.EndsWith(".dwt"))
            {
                return Path.GetDirectoryName(fullPath);
            }

            return String.Empty;
        }

        private static AcGe.Point3d ToGePoint3d(Tekla.Structures.Geometry3d.Point pt)
        {
            return new AcGe.Point3d(pt.X, pt.Y, pt.Z);
        }

        private static int CollectEdgesToBlock(Solid solid, AcDb.BlockTableRecord destBlock, AcDb.Transaction trans)
        {
            int edgeCount = 0;

            if (solid != null)
            {
                EdgeEnumerator edgeEnumerator = solid.GetEdgeEnumerator();

                while (edgeEnumerator.MoveNext())
                {
                    var edge = edgeEnumerator.Current as Edge;
                    if (edge != null)
                    {
                        var dbLine = new AcDb.Line(ToGePoint3d(edge.StartPoint), ToGePoint3d(edge.EndPoint));

                        destBlock.AppendEntity(dbLine);
                        trans.AddNewlyCreatedDBObject(dbLine, true);

                        edgeCount++;
                    }
                }
           }

           return edgeCount;
        }

    }
}
