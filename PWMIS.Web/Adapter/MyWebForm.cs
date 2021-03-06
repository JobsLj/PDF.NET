/*
 * ========================================================================
 * Copyright(c) 2006-2010 PWMIS, All Rights Reserved.
 * Welcom use the PDF.NET (PWMIS Data Process Framework).
 * See more information,Please goto http://www.pwmis.com/sqlmap 
 * ========================================================================
 * 该类的作用
 * 
 * 作者：邓太华     时间：2008-10-12
 * 版本：V4.5
 * 
 * 修改者：         时间：2012-11-1                
 * 修改说明：收集数据的时候，改进对SQLite的支持
 * ========================================================================
*/
using System;
using System.Data ;
using System.Collections ;
using System.Collections.Generic;
using System.Web;
using System.Web.UI ;
using PWMIS.DataProvider.Data ;
using PWMIS.Common;
using PWMIS.Web.Controls;

namespace PWMIS.DataProvider.Adapter
{
    /// <summary>
    /// 用户使用数据控件的自定义方法委托
    /// </summary>
    /// <param name="dataControl"></param>
    public delegate void  UseDataControl( IDataControl dataControl);

	/// <summary>
	/// 智能Web窗体数据处理类，包括数据收集和数据持久化（保存到数据库）等方法；如果使用事务请勿使用该类中间的静态方法。
	/// </summary>
	public class MyWebForm
	{
		private bool _CheckRowUpdateCount=false;//是否检查更新结果所影响的行数，如果检查，那么受影响的行不大于0将抛出错误。
		private CommonDB _dao=null;
		private static MyWebForm  _instance=null;
		/// <summary>
		/// 默认构造函数
		/// </summary>
		public MyWebForm()
		{
			//
			// TODO: 在此处添加构造函数逻辑
			//
		}

		/// <summary>
		/// 是否检查更新结果所影响的行数，如果检查，那么受影响的行不大于0将抛出错误。
		/// </summary>
		public bool CheckAffectRowCount
		{
			get
			{
				return _CheckRowUpdateCount;
			}
			set
			{
				_CheckRowUpdateCount=value;
			}
		}

		/// <summary>
		/// 智能Web窗体数据处理类 的静态实例
		/// </summary>
		public static  MyWebForm Instance
		{
			get
			{
				if(_instance==null)
					_instance=new MyWebForm ();
				return _instance;
			}
		}

		/// <summary>
		/// 获取或者设置数据访问对象，默认使用静态 CommonDB 类的实例对象，
		/// 如果使用事务并且有并发访问，请设置 CommonDB 的动态实例，例如：MyDB.GetDBHelper();
		/// </summary>
		public CommonDB DAO
		{
			get
			{
				if(_dao==null)
					_dao=MyDB.Instance ;
				return _dao;
			}
			set
			{
				_dao=value;
			}
		}

        public IBCommand IBCommand
        {
            get
            {
                throw new System.NotImplementedException();
            }
            set
            {
            }
        }

        public MyDB MyDB
        {
            get
            {
                throw new System.NotImplementedException();
            }
            set
            {
            }
        }

        public  IDataControl IDataControl
        {
            get
            {
                throw new System.NotImplementedException();
            }
            set
            {
            }
        }

		/// <summary>
		/// 清除窗体上的智能控件的值
		/// </summary>
		/// <param name="Controls">窗体控件集和</param>
		public static void ClearIBData(ControlCollection Controls)
		{
            //使用匿名委托
            UseDataControl clearData = delegate(IDataControl dataControl) 
            { 
                dataControl.SetValue(""); 
            };
            DoDataControls(Controls, clearData);

		}

        /// <summary>
        /// 使用自定义的方法处理控件集合中每一个智能窗体数据控件，使用户不必额外遍历控件集合。
        /// </summary>
        /// <param name="controls">窗体容器控件的控件集合</param>
        /// <param name="useMethod">自定义的方法</param>
        public static void DoDataControls(ControlCollection controls,UseDataControl useMethod)
        {
            foreach (IDataControl item in GetIBControls(controls))
                useMethod(item);
        }

        /// <summary>
        /// 从控件集合的每个元素及其子元素中寻找所有的智能数据控件并返回智能控件列表
        /// </summary>
        /// <param name="controls">控件集合，如页面容器对象</param>
        /// <returns>智能控件列表</returns>
        public static List<IDataControl>  GetIBControls(ControlCollection controls)
        {
            List<IDataControl> IBControls = new List<IDataControl>();
            findIBControls(IBControls, controls);
            return IBControls;
        }

		#region 页面数据收集

		/// <summary>
		/// 寻找智能控件，将其放到数组列表中
		/// </summary>
		/// <param name="arrIBs">存放控件的数组</param>
		/// <param name="controls">要寻找的原控件集合</param>
		private static void findIBControls(List<IDataControl > arrIBs,ControlCollection controls)
		{
			foreach(Control ctr in controls)
			{
				if(ctr is  IDataControl )
				{
					arrIBs.Add (ctr as IDataControl );
				}
				if(ctr.HasControls() )
				{
					findIBControls(arrIBs,ctr.Controls );
				}
			}
		
		}

		/// <summary>
		/// 获取选择和删除查询的SQL语句
		/// </summary>
		/// <param name="IBControls">已经收集的控件集合</param>
		/// <returns> ArrayList 中的成员为 IBCommand 对象，包含具体的CRUD SQL</returns>
        public static List<IBCommand> GetSelectAndDeleteCommand(List<IDataControl> IBControls)
		{
            List<IBCommand> IBCommands = new List<IBCommand>();
			//获取表单中的CRUD 语句。
			while(IBControls.Count >0)
			{
				string strTableName="";
				string strSelect="";
				string strDelete="";
				string strFields="";
				//string strValues="";
				
				string strCondition="";
				int nullCount=0;
				

				for(int i=0;i<IBControls.Count ;i++)// object objCtr in IBControls)
				{
					object objCtr=IBControls[i];
					if(objCtr!=null)
					{
						 IDataControl ibCtr =objCtr as  IDataControl;
						//只有非只读的控件才可以更新数据到数据库
						if(ibCtr!=null )
						{
							if(strTableName=="")
							{
								strTableName=ibCtr.LinkObject;
								strSelect="SELECT ";
								strDelete="DELETE FROM "+strTableName;
								
							}
							//找到当前处理的表，只读的字段也可以处理
							if(strTableName==ibCtr.LinkObject && ibCtr.LinkObject!="" )
							{
								string cValue=ibCtr.GetValue ().ToString ().Replace ("'","''");
								if(ibCtr.PrimaryKey )
								{
									if(cValue!="")
									{
										//防止SQL注入式攻击
										cValue=(ibCtr.SysTypeCode==System.TypeCode.String || ibCtr.SysTypeCode==System.TypeCode.DateTime  ? "'"+ cValue +"'":Convert.ToDouble (cValue).ToString ());
									}
									strCondition+=" And "+ibCtr.LinkProperty+"="+cValue;
								}
                                string temp = ibCtr.LinkProperty + ",";
                                if (temp.Trim() != "," && strFields.IndexOf(temp) == -1)
                                    strFields += temp;

                                IBControls[i] = null;
							}
                            if(ibCtr.LinkObject=="" || ibCtr.LinkProperty =="")
                                IBControls[i] = null;
							
						}
					}
					else
						nullCount++;
					
				}//end for

                if (strFields == "")
                    break;

				strSelect+=strFields.TrimEnd (',')+" FROM "+strTableName+" WHERE 1=1 "+strCondition;
				strDelete+=" WHERE 1=1 "+strCondition;

				IBCommand ibcmd=new IBCommand (strTableName);
				ibcmd.SelectCommand =strSelect ;
				ibcmd.DeleteCommand =strDelete ;
				
				IBCommands.Add (ibcmd);

                if (nullCount >= IBControls.Count - 1)
                    break;
			}//end while

			return IBCommands;
		}

		/// <summary>
		/// 获取选择和删除查询的SQL语句
		/// </summary>
		/// <param name="Controls">要收集的控件集合</param>
		/// <returns> ArrayList 中的成员为 IBCommand 对象，包含具体的CRUD SQL</returns>
		public static List<IBCommand> GetSelectAndDeleteCommand(ControlCollection Controls)
		{
            List<IDataControl> IBControls = new List<IDataControl>();
			findIBControls(IBControls ,Controls );
			return GetSelectAndDeleteCommand(IBControls);
		}

		/// <summary>
		/// 收集窗体中的智能控件，组合成能够直接用于数据库插入和更新 查询的 SQL语句
		/// 一个窗体中可以同时处理多个表的数据操作
        /// 如果控件的数据属性设置为只读，那么该控件的值不会更新到数据库；如果该控件的数据属性设置为主键，那么更新语句将附带该条件
		/// 邓太华 2008.1.15
		/// </summary>
		/// <returns>
		/// ArrayList 中的成员为 IBCommand 对象，包含具体的CRUD SQL
		///</returns>
        public static List<IBCommand> GetIBFormData(ControlCollection Controls,CommonDB DB)
		{
            List<IDataControl> IBControls = new List<IDataControl>();
			findIBControls(IBControls ,Controls );

            List<IBCommand> IBCommands = new List<IBCommand>();
			//获取表单中的CRUD 语句。
			while(IBControls.Count >0)
			{
				string strTableName="";
				string strInsert="";
				string strFields="";
				string strValues="";
				string strUpdate="";
				string strCondition="";
				int nullCount=0;
				int ID=-1;

                int paraIndex = 0;
                List<IDataParameter> paraList = new List<IDataParameter>();

				for(int i=0;i<IBControls.Count ;i++)// object objCtr in IBControls)
				{
					object objCtr=IBControls[i];
					if(objCtr!=null)
					{
						 IDataControl ibCtr =objCtr as  IDataControl;
						//只有非只读的控件才可以更新数据到数据库
						if(ibCtr!=null )
						{
							if(strTableName=="" && ibCtr.LinkObject!="")
							{
								strTableName=ibCtr.LinkObject;
								strInsert="INSERT INTO ["+strTableName+"](";
								strUpdate="UPDATE ["+strTableName+"] SET ";
							}
							//找到当前处理的表，只有非只读的字段可以更新
							if(strTableName==ibCtr.LinkObject && ibCtr.LinkProperty!="" )
                            {
                                #region 原来获取值得方法
                                //string cValue=ibCtr.GetValue ().ToString ().Replace ("'","''");
								
                                ////dth,2008.4.11 处理字符串类型为空的情况
                                ////防止SQL注入式攻击
                                ////不论是否为空都进行字符串类型测试
                                //if(ibCtr.SysTypeCode==System.TypeCode.String || ibCtr.SysTypeCode==System.TypeCode.Empty)
                                //{
                                //    cValue="'"+ cValue +"'";
                                //}
                                //else
                                //{
                                //    if(cValue!="")
                                //    {
                                //        if(ibCtr.SysTypeCode==System.TypeCode.Boolean )
                                //            cValue=(cValue.ToUpper ()=="TRUE"?"1":"0");
                                //        else if(ibCtr.SysTypeCode==System.TypeCode.DateTime )
                                //        {
                                //            if (DB.CurrentDBMSType == DBMSType.SQLite)
                                //                cValue = "'" + DateTime.Parse(cValue).ToString("s") + "'";
                                //            else
                                //                cValue = "'" + cValue + "'";//SQL SERVER 日期格式
                                        
                                //        }
                                //        else if(ibCtr.SysTypeCode==System.TypeCode.DBNull )
                                //        {
                                //            cValue="NULL";
                                //        }
                                //        else if(ibCtr.SysTypeCode==System.TypeCode.Object )
                                //        {
                                //            //Object 标记不做任何处理，例如可以使用最大值加一获取主键值
											
                                //        }
                                //        else if(!(ibCtr.SysTypeCode==System.TypeCode.String || ibCtr.SysTypeCode==System.TypeCode.Empty))
                                //        {
                                //            //如果不是字符串那么试图进行数字转换
                                //            cValue=Convert.ToDouble (cValue).ToString ();
                                //        }

                                //    }
                                //}
                                #endregion

                                string cValue = string.Empty;
                                object ctlValue = ibCtr.GetValue();

                                //非只读的数据才更新
                                if (ctlValue!=DBNull.Value)
								{
                                    cValue = DB.GetParameterChar+ "P"+paraIndex++;
                                    IDataParameter para = DB.GetParameter(cValue, ctlValue);
                                    if (ibCtr.SysTypeCode == System.TypeCode.String || ibCtr.SysTypeCode == System.TypeCode.Empty)
                                    {
                                        if (ibCtr is DataTextBox)
                                        {
                                            int maxStringLength = ((DataTextBox)ibCtr).MaxLength;
                                            ((IDbDataParameter)para).Size = maxStringLength;
                                        }
                                    }
                                    paraList.Add(para);

                                    //2010.1.25 取消 ibCtr.PrimaryKey 不能更新的限制，例如可以让GUID主键列可以更新
                                    //如果是自增列，设置该列的控件属性为 只读属性即可。
                                    if (!ibCtr.ReadOnly) 
									{
										strFields+=ibCtr.LinkProperty +",";
										strValues+=cValue+",";
										strUpdate+=ibCtr.LinkProperty+"="+cValue+",";
									}
                                    if (ibCtr.PrimaryKey) //只要是主键就作为更新的条件
                                    {
                                        strCondition += " And " + ibCtr.LinkProperty + "=" + cValue;
                                        if (ibCtr.SysTypeCode == System.TypeCode.Int32)
                                            ID = int.Parse(ctlValue.ToString());
                                        else
                                            ID = -2;//主键可能是非数字型
                                       
                                    }
								}
								

							}
							IBControls[i]=null;
						}
					}
					else
						nullCount++;
					
				}//end for

				if(nullCount>=IBControls.Count-1)
					break;

				strInsert+=strFields.TrimEnd (',')+") VALUES ("+strValues.TrimEnd (',')+")";
				strUpdate=strUpdate.TrimEnd (',')+" WHERE 1=1 "+strCondition;

				IBCommand ibcmd=new IBCommand (strTableName);
				ibcmd.InsertCommand=strInsert ;
				ibcmd.UpdateCommand =strUpdate ;
                //if( ID>0) 
                    ibcmd.InsertedID =ID;
                    ibcmd.Parameters = paraList.ToArray();

				IBCommands.Add (ibcmd);
			}//end while

			return IBCommands;
			
		}

		#endregion

		#region 数据填充以及持久化数据

	
        /// <summary>
        /// 自动更新窗体数据
        /// </summary>
        /// <param name="Controls">控件集合</param>
        /// <returns></returns>
        public List<IBCommand> AutoUpdateIBFormData(ControlCollection Controls)
		{
			List<IBCommand > ibCommandList=GetIBFormData(Controls,this.DAO);
			int result=0;
			foreach(object item in ibCommandList)
			{
				IBCommand command=(IBCommand)item;
				if(command.InsertedID >0)
					result=DAO.ExecuteNonQuery (command.UpdateCommand,CommandType.Text,command.Parameters );//修改未合并
                else if (command.InsertedID ==-2)
                {
                    result = DAO.ExecuteNonQuery(command.UpdateCommand, CommandType.Text, command.Parameters);
                    if (result <= 0)
                    {
                        //2013-3-8 已经不需要克隆，看CommandDB
                        ////参数已经被使用，必须克隆一份
                        //IDataParameter[] paras = new IDataParameter[command.Parameters.Length];
                        //int index = 0;
                        //foreach (IDataParameter old in command.Parameters)
                        //{
                        //    paras[index++] = (IDataParameter)((ICloneable)old).Clone();
                        //}
                        ////
                        //result = DAO.ExecuteNonQuery(command.InsertCommand, CommandType.Text, paras);
                        result = DAO.ExecuteNonQuery(command.InsertCommand, CommandType.Text, command.Parameters);
                    }
                }
				else
				{
					object id=0;
					result=DAO.ExecuteInsertQuery (command.InsertCommand,CommandType.Text,command.Parameters,ref id);
					command.InsertedID=Convert.ToInt32 (id);
				}
				if(result<=0 && _CheckRowUpdateCount)
					throw new Exception ("在更新表"+command.TableName +"中未取得受影响的行数，数据错误信息："+DAO.ErrorMessage );
					
			}
			return ibCommandList;
		}

        /// <summary>
        /// 自动更新含有GUID主键或字符型主键的窗体数据，注该控件必须设置PrimaryKey属性
        /// </summary>
        /// <param name="Controls">控件集合</param>
        /// <param name="guidControl">Gudi或字符型主键控件</param>
        /// <returns>更新是否成功</returns>
        public bool  AutoUpdateIBFormData(ControlCollection Controls,  IDataControl guidControl)
        {
            object  guidObj=guidControl.GetValue();
            if (guidObj == null || guidObj.ToString() == "")
                throw new Exception("GUID 主键或字符型主键列更新数据不能为空！");
            if (guidControl.ReadOnly  )
                throw new Exception("GUID 主键或字符型主键列更新数据时不能设置为只读！");
            if (!guidControl.PrimaryKey )
                throw new Exception("GUID 主键或字符型主键列更新数据时必须设置PrimaryKey属性！");

            string guidText = guidObj.ToString();
            List<IBCommand> ibCommandList = GetIBFormData(Controls,this.DAO);
            int result = 0;
            foreach (IBCommand command in ibCommandList)
            {
                if (command.TableName == guidControl.LinkObject)
                {
                    string sql = "select " + guidControl.LinkProperty + " from " + guidControl.LinkObject + " where " + guidControl.LinkProperty + "='" + guidText + "'";
                    object guidInDb = DAO.ExecuteScalar(sql);
                    if (guidInDb != null && guidInDb.ToString() == guidText)
                    {
                        //在数据库中有该记录
                        result = DAO.ExecuteNonQuery(command.UpdateCommand, CommandType.Text, command.Parameters);
                    }
                    else
                    {
                        result = DAO.ExecuteNonQuery(command.InsertCommand, CommandType.Text, command.Parameters);
                    }
                    return result>0 ;
                }

            }
            return false;
        }

		/// <summary>
		/// 自动填充智能窗体控件的数据
		/// </summary>
		/// <param name="Controls">要填充的窗体控件集和</param>
		public void AutoSelectIBForm(ControlCollection Controls)
		{
            List<IDataControl> IBControls = new List<IDataControl>();
			findIBControls(IBControls ,Controls );

            List<IDataControl> IBControls2 = new List<IDataControl>();
            foreach (IDataControl obj in IBControls)
			{
				IBControls2.Add (obj);
			}
			//IBControls2 将会被请空
			List<IBCommand> ibCommandList=GetSelectAndDeleteCommand(IBControls2);

            foreach (IBCommand command in ibCommandList)
			{
				IDataReader reader=DAO.ExecuteDataReaderWithSingleRow (command.SelectCommand );
				if(reader!=null && reader.Read ())
				{
					foreach(object obj in IBControls)
					{
						 IDataControl ibCtr =obj as  IDataControl;
						if(ibCtr.LinkObject ==command.TableName )
						{
							for(int i=0;i<reader.FieldCount ;i++)
							{
								if(reader.GetName(i)==ibCtr.LinkProperty )
								{
									ibCtr.SetValue (reader[i]);
									break;
								}
							}
						}
					}
				}
				//应该在此处关闭阅读器，否则在事物中出现Command对象繁忙的问题
				reader.Close ();
			}
		}

        /// <summary>
        /// 从数据集DataSet填充数据到数据控件上面，DataSet中的表名称必须和数据控件的LinkObject匹配（不区分大小写）
        /// </summary>
        /// <param name="Controls">要填充的窗体控件集和</param>
        /// <param name="dsSource">提供属于源的数据集</param>
        public void AutoSelectIBForm(ControlCollection Controls,DataSet dsSource)
        {
            List<IDataControl> IBControls = new List<IDataControl>();
            findIBControls(IBControls, Controls);

            foreach (DataTable dt in dsSource .Tables )
            {
                string tableName=dt.TableName;
                foreach (object obj in IBControls)
                    {
                         IDataControl ibCtr = obj as  IDataControl;
                        if (string.Compare( ibCtr.LinkObject, tableName,true )==0)
                        {
                            for (int i = 0; i < dt.Columns.Count ; i++)
                            {
                                if (string.Compare (dt.Columns[i].ColumnName,ibCtr.LinkProperty,true )==0 )
                                {
                                    ibCtr.SetValue(dt.Rows[0][i]);
                                    break;
                                }
                            }
                        }
                    }
            }

        }

        /// <summary>
        /// 从实体类填充数据到页面控件
        /// </summary>
        /// <param name="Controls"></param>
        /// <param name="entity"></param>
        public void AutoSelectIBForm(ControlCollection Controls,  IEntity entity)
        {
            List<IDataControl> IBControls = new List<IDataControl>();
            findIBControls(IBControls, Controls);

            foreach (object obj in IBControls)
            {
                 IDataControl ibCtr = obj as  IDataControl;
                foreach (string key in entity.PropertyNames )
                {
                    if (string.Compare(key, ibCtr.LinkProperty, true) == 0)
                    {
                        ibCtr.SetValue(entity.PropertyList(key));
                        break;
                    }
                }
                
            }

        }


		/// <summary>
        /// 自动删除智能窗体控件的持久化数据
		/// </summary>
        /// <param name="Controls">要处理的窗体控件集和</param>
		/// <returns>操作受影响的记录行数</returns>
		public int AutoDeleteIBForm(ControlCollection Controls)
		{
            List<IDataControl> IBControls = new List<IDataControl>();
			findIBControls(IBControls ,Controls );

            List<IDataControl> IBControls2 = new List<IDataControl>();
            foreach (IDataControl obj in IBControls)
			{
				IBControls2.Add (obj);
			}
			//IBControls2 将会被请空
			List<IBCommand> ibCommandList=GetSelectAndDeleteCommand(IBControls2);
            int count = 0;
            foreach (IBCommand command in ibCommandList)
			{
                count+=DAO.ExecuteNonQuery(command.DeleteCommand);
			}
            return count;
			
		}

		#endregion
	}
}
