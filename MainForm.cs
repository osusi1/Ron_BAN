using System;
using System.Windows.Forms;
using System.IO;
using System.Text;
using System.Timers;
using System.Threading.Tasks;
using System.Drawing;

using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;

namespace Ron_BAN
{
	public partial class MainForm : Form
	{
		// リソースの節約
		static public Font ms_meiryo_Ke_P_9pt = null;
		static public Font ms_meiryo_Ke_P_8pt = null;
		static public Font ms_meiryo_8pt = null;

		// タイマ関連
		bool mb_timer_enabled = false;
		System.Timers.Timer m_Timer_getJSON = null;
		uint m_timer_elapsed_msec = 0;
		uint m_timer_interval_msec = 1700;

//System.Timers.Timer m_Timer_delayBAN = null;
		const int MSEC_timer_intvl_delayBAN = 5000;

		const uint c_SEC_intvl_confirmation = 20 * 60;  // 20分毎に接続確認
		uint m_nextsec_cnct_onfirmation = c_SEC_intvl_confirmation;

		// ------------------------------------------------------------------------------------
		public MainForm()
		{
			InitializeComponent();

			// リソース節約のためのコード
			ms_meiryo_Ke_P_9pt = new Font("MeiryoKe_PGothic", 9F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(128)));

			m_Btn_connect.Font = ms_meiryo_Ke_P_9pt;
			m_Btn_postMsg.Font = ms_meiryo_Ke_P_9pt;
			m_Btn_Chg_RmLimit.Font = ms_meiryo_Ke_P_9pt;
			m_Btn_timer_JSON.Font = ms_meiryo_Ke_P_9pt;

			m_TBox_PostMsg.Font = ms_meiryo_Ke_P_9pt;
			m_Lbl_timer_elapsed.Font = ms_meiryo_Ke_P_9pt;
			label1.Font = ms_meiryo_Ke_P_9pt;

			m_btn_test_1.Font = ms_meiryo_Ke_P_9pt;
			m_btn_test_2.Font = ms_meiryo_Ke_P_9pt;
			m_btn_test_3.Font = ms_meiryo_Ke_P_9pt;

			ms_meiryo_Ke_P_8pt = new Font("MeiryoKe_PGothic", 8.25F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(128)));

			m_TBox_uname.Font = ms_meiryo_Ke_P_8pt;
			m_TBox_roomname.Font = ms_meiryo_Ke_P_8pt;
			label2.Font = ms_meiryo_Ke_P_8pt;
			label3.Font = ms_meiryo_Ke_P_8pt;
			label4.Font = ms_meiryo_Ke_P_8pt;

			ms_meiryo_8pt = new Font("メイリオ", 8.25F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(128)));

			m_RBox_usrMsg.Font = ms_meiryo_8pt;
			m_RBox_status.Font = ms_meiryo_8pt;

			m_CmbBox_str_icon.Font = ms_meiryo_8pt;

			// --------------------------------------------

			m_Timer_getJSON = new System.Timers.Timer(m_timer_interval_msec);
			m_Timer_getJSON.Elapsed += OnTimer_GetJSON;
			m_Timer_getJSON.AutoReset = true;
			m_Timer_getJSON.SynchronizingObject = this;
/*
			m_Timer_delayBAN = new System.Timers.Timer(MSEC_timer_intvl_delayBAN);
			m_Timer_delayBAN.Elapsed += OnTimer_delayBAN;
			m_Timer_delayBAN.AutoReset = true;
			m_Timer_delayBAN.SynchronizingObject = this;
*/
			ms_RBox_status = m_RBox_status;
			m_RBox_status.SelectionTabs = new int[] { 30 };
			m_RBox_status.LanguageOption = RichTextBoxLanguageOptions.UIFonts;  // 行間を狭くする

			ms_RBox_usrMsg = m_RBox_usrMsg;
			ms_RBox_usrMsg.SelectionTabs = new int[] { 30 };
			ms_RBox_usrMsg.LanguageOption = RichTextBoxLanguageOptions.UIFonts;  // 行間を狭くする

			// BanCtrl を生成
			BanCtrls_Ctrlr.Init();
			// BanCtrl の GUI を生成
			this.Create_BanCtrls_GUI();

			// encip の記録ファイルを読み込む
			DB_static.Load_fromFile();

			MainForm.WriteStatus("--- 起動しました\r\n");
		}

		~MainForm()
		{
			if (mb_timer_enabled) { m_Timer_getJSON.Stop(); }
			m_Timer_getJSON.Dispose();

			Drrr_Host2.Dispose();
		}

		// ------------------------------------------------------------------------------------

		static RichTextBox ms_RBox_usrMsg = null;
		public static void WriteMsg(string msg)
		{
			ms_RBox_usrMsg.SelectionColor = System.Drawing.Color.FromArgb(0, 150, 255);
			ms_RBox_usrMsg.AppendText(DateTime.Now.ToString("[HH:mm:ss]　"));
			ms_RBox_usrMsg.SelectionColor = System.Drawing.Color.Black;
			ms_RBox_usrMsg.AppendText(msg);
		}

		// ------------------------------------------------------------------------------------

		static RichTextBox ms_RBox_status = null;
		public static void WriteStatus(string msg)
		{
			ms_RBox_status.SelectionColor = System.Drawing.Color.FromArgb(255, 100, 50);
			ms_RBox_status.AppendText(DateTime.Now.ToString("[HH:mm:ss]　"));
			ms_RBox_status.SelectionColor = System.Drawing.Color.Black;
			ms_RBox_status.AppendText(msg);
		}
		
		// ------------------------------------------------------------------------------------
		bool mb_connected_once = false;

		async void OnClk_ConnectBtn(object sender, EventArgs e)
		{
			string str_uname = m_TBox_uname.Text;
			string str_room_name = m_TBox_roomname.Text;

			if (str_uname.Length == 0 || m_CmbBox_str_icon.SelectedIndex < 0 || str_room_name.Length == 0)
			{
				MessageBox.Show("! ユーザ名等に空欄があります。");
				return;
			}

			try
			{
				if (mb_connected_once == false)
				{
					mb_connected_once = true;
					m_Btn_connect.Text = "切断";

					// 接続処理開始
					// アイコン名は girl, moza, tanaka, kanra, usa, gg, orange, zaika, 
					// setton, zawa, neko, purple, kai, bakyura, neko2, numakuro など
					string ret_str = await Drrr_Host2.Establish_cnct(
													str_uname, m_CmbBox_str_icon.SelectedItem.ToString(), str_room_name);
					if (ret_str != null)
					{
						WriteStatus($"+++ 失敗メッセージ： {ret_str}\r\n\r\n");
						throw new Exception("!!! 部屋の作成に失敗しました。");
					}

					StartTimer_GetJSON();
				}
				else
				{
					m_Timer_getJSON.Stop();
					m_Btn_timer_JSON.Enabled = false;

					m_Btn_connect.Text = "切断済み";
					m_Btn_connect.Enabled = false;

					// 切断処理開始
					string ret_str = await Drrr_Host2.Disconnect();
					if (ret_str != null)
					{
						WriteStatus($"+++ 失敗メッセージ： {ret_str}\r\n\r\n");
						throw new Exception("!!! 切断処理に失敗しました。");
					}
				}
			}
			catch (Exception ex)
			{
				WriteStatus(ex.ToString() + "\r\n\r\n");

				mb_connected_once = true;
				m_Btn_connect.Text = "エラー発生";
				m_Btn_connect.Enabled = false;
			}
		}

		// ------------------------------------------------------------------------------------

		async void OnClk_PostMsgBtn(object sender, EventArgs e)
		{
			try
			{
				bool b_ret = await PostMsg(m_TBox_PostMsg.Text);
				if (b_ret) { m_TBox_PostMsg.Clear(); }
			}
			catch (Exception ex)
			{
				MainForm.WriteStatus(ex.ToString() + "\r\n");
			}
		}

		bool mb_postMsg = false;
		async Task<bool> PostMsg(string msg_to_post)  // 戻り値は、「m_TBox_PostMsg.Clear()」のためだけに利用される
		{
			if (mb_postMsg) { return false; }
			m_Btn_postMsg.Enabled = false;
			mb_postMsg = true;

			try
			{
				Drrr_Host2.HttpTask postMsg_task = Drrr_Host2.PostMsg_Task_Factory.Create(msg_to_post);
				await postMsg_task.DoWork();

				mb_postMsg = false;
				m_Btn_postMsg.Enabled = true;

				if (postMsg_task.m_str_cancel != null)
				{
					MainForm.WriteStatus($"!!! 「PostMsg」がキャンセルされました。\r\n{postMsg_task.m_str_cancel}");
					return false;
				}

				MainForm.WriteStatus("+++ PostMsg 成功\r\n");
				return true;
			}
			catch (Exception ex)
			{
				MainForm.WriteStatus(ex.ToString() + "\r\n");
				mb_postMsg = false;
				m_Btn_postMsg.Enabled = true;

				return false;
			}
		}

		// ------------------------------------------------------------------------------------
/*
		void OnClk_Btn_GetJSON(object sender, EventArgs e)
		{
			GetJSON(DateTime.Now);
		}
*/
		void OnClk_Btn_Toggle_Timer(object sender, EventArgs e)
		{
			if (mb_timer_enabled)
			{
				mb_timer_enabled = false;
				m_Btn_timer_JSON.Text = "タイマ開始";
				m_Timer_getJSON.Stop();
			}
			else
			{
				StartTimer_GetJSON();
				// 開始ボタンを押した直後に GetJSON を一度実行しておく
				GetJSON(DateTime.Now);
			}
		}

		void StartTimer_GetJSON()
		{
			if (mb_timer_enabled) { return; }

			mb_timer_enabled = true;
			m_Btn_timer_JSON.Text = "タイマ停止";
			m_Timer_getJSON.Start();
		}

		// タイマーで呼び出される関数
		// CS4014: 呼び出しの結果に 'await' 演算子を適用することを検討してください
		#pragma warning disable CS4014
		async void OnTimer_GetJSON(Object src, ElapsedEventArgs ev_time)
		{
			m_timer_elapsed_msec += m_timer_interval_msec;
			uint elapsed_sec = m_timer_elapsed_msec / 1000;
			m_Lbl_timer_elapsed.Text = Convert.ToString(elapsed_sec);
			GetJSON(ev_time.SignalTime);

			if (elapsed_sec > m_nextsec_cnct_onfirmation)
			{
				string str_post = $"--- 接続確認 {Convert.ToString(elapsed_sec)}秒\r\n";
				MainForm.WriteStatus(str_post);
				m_nextsec_cnct_onfirmation += c_SEC_intvl_confirmation;

				try
				{
					bool b_ret = await PostMsg(str_post);
					if (b_ret == false)
					{
						MainForm.WriteStatus("+++ PostMsg() が失敗したため、一度だけ再試行します。");
						PostMsg(str_post);  // この実行結果を待つ必要がないため、await は使用しない
					}
				}
				catch (Exception ex)
				{
					MainForm.WriteStatus(ex.ToString() + "\r\n");
				}
			}
		}
		#pragma warning restore CS4014

		bool mb_getJSON = false;  // await Drrr_Host2.GetJSON() で、awaiter が溜まるのを防ぐ
		async void GetJSON(DateTime datetime)
		{
			if (mb_getJSON) { return; }
			m_Btn_connect.Enabled = false;
			mb_getJSON = true;

			// --------------------------------------------------
			// JOSN の取得
			byte[] bytes_utf8;
			try
			{
				Drrr_Host2.HttpTask getJSON_task = Drrr_Host2.GetJSON_Task_Factory.Create();
				await getJSON_task.DoWork();

				mb_getJSON = false;
				m_Btn_connect.Enabled = true;

				if (getJSON_task.m_str_cancel != null)
				{
					MainForm.WriteStatus($"!!! 「JSON取得」がキャンセルされました。\r\n{getJSON_task.m_str_cancel}");
					return;
				}

				bytes_utf8 = await getJSON_task.m_http_res.Content.ReadAsByteArrayAsync();
			}
			catch (Exception ex)
			{
				MainForm.WriteStatus(ex.ToString() + "\r\n");
				mb_getJSON = false;
				m_Btn_connect.Enabled = true;
				return;
			}

			// --------------------------------------------------
			// JOSN の解析
			try
			{
				StringBuilder sb = DB_cur.Anlz_RoomJSON(bytes_utf8);
				if (sb.Length > 0) { MainForm.WriteMsg(sb.ToString()); }

				Update_BanCtrls();

				// 新規入室者へのメッセージ処理
				if (UInfo_onRoom.msb_dtct_new_usr)
				{
					UInfo_onRoom.msb_dtct_new_usr = false;

					try
					{
						Drrr_Host2.HttpTask postmsg_task = Drrr_Host2.PostMsg_Task_Factory.Create(
								"来室者用自動メッセージ：この部屋の趣旨は、以下の URL を参考にしてください。宜しくお願いします。");
						await postmsg_task.DoWork();

						if (postmsg_task.m_str_cancel != null)
						{ throw new Exception(postmsg_task.m_str_cancel); }

						await Task.Delay(750);

						postmsg_task = Drrr_Host2.PostMsg_Task_Factory.Create("https://github.com/kn201905/Ron_BAN");
						await postmsg_task.DoWork();

						if (postmsg_task.m_str_cancel != null)
						{ throw new Exception(postmsg_task.m_str_cancel); }
					}
					catch (Exception ex)
					{
						MainForm.WriteStatus($"!!! 来室者用メッセージの出力時にエラーが発生しました。\r\n{ex.ToString()}\r\n\r\n");
					}
				}
			}
			catch (Exception ex)
			{
				MainForm.WriteStatus($"!!! JSON解析中に例外が発生しました。JSON を保存します。\r\n{ex.ToString()}\r\n\r\n");

				File.WriteAllText(@"Z:err_" + datetime.ToString("HH_mm_ss_f") + ".json"
										, Encoding.UTF8.GetString(bytes_utf8));
			}
		}

		// ------------------------------------------------------------------------------------
		async void OnClk_Btn_Chg_RmLimit(object sender, EventArgs e)
		{
			try
			{
				string str_num = m_TBox_num_RmLimit.Text;
				int num_RmLimit = int.Parse(str_num);
				if (num_RmLimit < 2) { num_RmLimit = 2; }
				else if (num_RmLimit > 15) { num_RmLimit = 15; }

				Drrr_Host2.HttpTask chg_RmLimit_task = Drrr_Host2.Chg_RmLimit_Task_Factory.Create(num_RmLimit);
				await chg_RmLimit_task.DoWork();

				if (chg_RmLimit_task.m_str_cancel != null)
				{
					MainForm.WriteStatus($"!!! 「Chg_RmLimit」がキャンセルされました。\r\n{chg_RmLimit_task.m_str_cancel}");
					return;
				}

				MainForm.WriteStatus("+++ Chg_RmLimit 成功\r\n");
			}
			catch (Exception ex)
			{
				MainForm.WriteStatus(ex.ToString() + "\r\n");
			}
		}

		// ------------------------------------------------------------------------------------
		private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			DB_static.Save_toFile();
		}

		///////////////////////////////////////////////////////////////////////////////////////
		// テスト１
		void m_btn_test_1_Click(object sender, EventArgs e)
		{
		}

		// ------------------------------------------------------------------------------------
		// テスト２
		void m_btn_test_2_Click(object sender, EventArgs e)
		{
		}

		// ------------------------------------------------------------------------------------
		// テスト３
		void m_btn_test_3_Click(object sender, EventArgs e)
		{
		}
	}
}

