/*
VideoPolicy.h
Copyright (C) 2015  Belledonne Communications, Grenoble, France
This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.
This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.
You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
*/

#pragma once

#include "Utils.h"

namespace BelledonneCommunications
{
	namespace Linphone
	{
		namespace Native
		{
			ref class LinphoneCore;

			/// <summary>
			/// Class describing policy regarding video streams establishments.
			/// </summary>
			public ref class VideoPolicy sealed
			{
			public:
				/// <summary>
				/// Creates a default VideoPolicy object (automatically initiate and accept).
				/// </summary>
				/// <returns>The created VideoPolicy</returns>
				VideoPolicy();

				/// <summary>
				/// Creates a VideoPolicy object.
				/// </summary>
				/// <param name="automaticallyInitiate">Whether to activate video for outgoing calls</param>
				/// <param name="automaticallyAccept">Whether to accept video for incoming calls</param>
				/// <returns>The created VideoPolicy</returns>
				VideoPolicy(bool automaticallyInitiate, bool automaticallyAccept);

				/// <summary>
				/// Whether video shall be automatically proposed for outgoing calls.
				/// </summary>
				property bool AutomaticallyInitiate
				{
					bool get();
					void set(bool value);
				}

				/// <summary>
				/// Whether video shall be automatically accepted for incoming calls.
				/// </summary>
				property bool AutomaticallyAccept
				{
					bool get();
					void set(bool value);
				}

			private:
				friend class Utils;
				friend ref class LinphoneCore;

				bool automaticallyInitiate;
				bool automaticallyAccept;
			};
		}
	}
}